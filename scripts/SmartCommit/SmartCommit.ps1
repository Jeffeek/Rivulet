param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Claude", "Gemini", "OpenAI", "Auto")]
    [string]$Provider = "Auto",

    [Parameter(Mandatory=$false)]
    [string]$ApiKey = "",

    [Parameter(Mandatory=$false)]
    [string]$ConfigFile = ""
)

if ($ConfigFile -eq "") {
    $ConfigFile = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) ".smartcommit.config.json"
}

$ErrorActionPreference = "Stop"

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "  AI-Powered Commit Assistant" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Load configuration
$config = @{
    provider = "Claude"
    apiKeys = @{
        claude = $env:ANTHROPIC_API_KEY
        gemini = $env:GOOGLE_API_KEY
        openai = $env:OPENAI_API_KEY
    }
    models = @{
        claude = "claude-3-5-sonnet-20241022"
        gemini = "gemini-2.0-flash-exp"
        openai = "gpt-4o"
    }
}

# Load config file if exists
if (Test-Path $ConfigFile) {
    Write-Host "Loading configuration from $ConfigFile..." -ForegroundColor Gray
    $fileConfig = Get-Content $ConfigFile -Raw | ConvertFrom-Json

    if ($fileConfig.provider) { $config.provider = $fileConfig.provider }
    if ($fileConfig.apiKeys.claude) { $config.apiKeys.claude = $fileConfig.apiKeys.claude }
    if ($fileConfig.apiKeys.gemini) { $config.apiKeys.gemini = $fileConfig.apiKeys.gemini }
    if ($fileConfig.apiKeys.openai) { $config.apiKeys.openai = $fileConfig.apiKeys.openai }
    if ($fileConfig.models.claude) { $config.models.claude = $fileConfig.models.claude }
    if ($fileConfig.models.gemini) { $config.models.gemini = $fileConfig.models.gemini }
    if ($fileConfig.models.openai) { $config.models.openai = $fileConfig.models.openai }
}

# Override with parameter if provided
if ($Provider -ne "Auto") {
    $config.provider = $Provider
}

# Auto-detect provider if set to Auto
if ($Provider -eq "Auto") {
    if ($config.apiKeys.claude) {
        $config.provider = "Claude"
    } elseif ($config.apiKeys.gemini) {
        $config.provider = "Gemini"
    } elseif ($config.apiKeys.openai) {
        $config.provider = "OpenAI"
    } else {
        Write-Host "Error: No API keys found! Please set one of:" -ForegroundColor Red
        Write-Host "  - ANTHROPIC_API_KEY (for Claude)" -ForegroundColor Yellow
        Write-Host "  - GOOGLE_API_KEY (for Gemini)" -ForegroundColor Yellow
        Write-Host "  - OPENAI_API_KEY (for OpenAI)" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Or create a config file: $ConfigFile" -ForegroundColor Yellow
        Write-Host ""
        exit 1
    }
}

# Override API key if provided as parameter
if ($ApiKey) {
    switch ($config.provider) {
        "Claude" { $config.apiKeys.claude = $ApiKey }
        "Gemini" { $config.apiKeys.gemini = $ApiKey }
        "OpenAI" { $config.apiKeys.openai = $ApiKey }
    }
}

# Validate API key for selected provider
$currentApiKey = switch ($config.provider) {
    "Claude" { $config.apiKeys.claude }
    "Gemini" { $config.apiKeys.gemini }
    "OpenAI" { $config.apiKeys.openai }
}

if (-not $currentApiKey) {
    Write-Host "Error: No API key found for provider '$($config.provider)'!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Set the appropriate environment variable:" -ForegroundColor Yellow
    switch ($config.provider) {
        "Claude" {
            Write-Host "  `$env:ANTHROPIC_API_KEY = 'your-api-key'" -ForegroundColor Gray
            Write-Host "  Get key at: https://console.anthropic.com/" -ForegroundColor Cyan
        }
        "Gemini" {
            Write-Host "  `$env:GOOGLE_API_KEY = 'your-api-key'" -ForegroundColor Gray
            Write-Host "  Get key at: https://aistudio.google.com/apikey" -ForegroundColor Cyan
        }
        "OpenAI" {
            Write-Host "  `$env:OPENAI_API_KEY = 'your-api-key'" -ForegroundColor Gray
            Write-Host "  Get key at: https://platform.openai.com/api-keys" -ForegroundColor Cyan
        }
    }
    Write-Host ""
    exit 1
}

Write-Host "Provider: $($config.provider)" -ForegroundColor Green
Write-Host "Model:    $($config.models[$config.provider.ToLower()])" -ForegroundColor Green
Write-Host ""

# Navigate to repository root (2 levels up from scripts/SmartCommit/)
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location (Join-Path $ScriptDir "../..")

# Check for uncommitted changes
Write-Host "Checking for changes..." -ForegroundColor Yellow
$status = git status --porcelain
if (-not $status) {
    Write-Host "No changes to commit. Working tree is clean." -ForegroundColor Green
    Write-Host ""
    exit 0
}

# Show current status
Write-Host ""
Write-Host "Current changes:" -ForegroundColor Yellow
git status --short
Write-Host ""

# Check if there are staged changes
$stagedChanges = git diff --cached --name-only
$unstagedChanges = git diff --name-only

if (-not $stagedChanges -and $unstagedChanges) {
    Write-Host "No staged changes found. Stage changes first?" -ForegroundColor Yellow
    Write-Host "  [a] Stage all changes (git add .)" -ForegroundColor Gray
    Write-Host "  [s] Skip and exit (stage manually)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Choice [a/s]: " -NoNewline -ForegroundColor Cyan
    $stageChoice = Read-Host

    if ($stageChoice -eq 'a' -or $stageChoice -eq 'A') {
        Write-Host ""
        Write-Host "Staging all changes..." -ForegroundColor Yellow
        git add .
    } else {
        Write-Host ""
        Write-Host "Exiting. Please stage your changes manually with 'git add'." -ForegroundColor Yellow
        Write-Host ""
        exit 0
    }
}

# Get the diff of staged changes
Write-Host ""
Write-Host "Analyzing changes..." -ForegroundColor Yellow
$diff = git diff --cached

if (-not $diff) {
    Write-Host "Error: No staged changes to commit." -ForegroundColor Red
    Write-Host ""
    exit 1
}

# Get recent commit messages for context
$recentCommits = git log -5 --pretty=format:"%s" 2>$null
$commitContext = if ($recentCommits) { $recentCommits -join "`n" } else { "" }

# System prompt for all providers
$systemPrompt = @"
You are an expert at writing clear, concise git commit messages following best practices.

Guidelines:
- First line: brief summary (50 chars or less), imperative mood (e.g., "Add", "Fix", "Update", "Remove")
- Blank line, then detailed explanation if needed
- Focus on WHY the change was made, not just WHAT changed
- Use bullet points for multiple changes
- Be specific and meaningful
- Do not use emojis

Example format:
Add user authentication feature

- Implement JWT-based authentication
- Add login and registration endpoints
- Include password hashing with bcrypt
"@

# Function to call Claude API
function Invoke-ClaudeAPI {
    param(
        [string]$Prompt,
        [string]$ApiKey,
        [string]$Model
    )

    $body = @{
        model = $Model
        max_tokens = 1024
        messages = @(
            @{
                role = "user"
                content = $Prompt
            }
        )
        system = $systemPrompt
    } | ConvertTo-Json -Depth 10

    $headers = @{
        "x-api-key" = $ApiKey
        "anthropic-version" = "2023-06-01"
        "content-type" = "application/json"
    }

    $response = Invoke-RestMethod -Uri "https://api.anthropic.com/v1/messages" `
        -Method Post `
        -Headers $headers `
        -Body $body `
        -TimeoutSec 30

    return $response.content[0].text.Trim()
}

# Function to call Gemini API
function Invoke-GeminiAPI {
    param(
        [string]$Prompt,
        [string]$ApiKey,
        [string]$Model
    )

    $combinedPrompt = "$systemPrompt`n`n$Prompt"

    $body = @{
        contents = @(
            @{
                parts = @(
                    @{
                        text = $combinedPrompt
                    }
                )
            }
        )
        generationConfig = @{
            maxOutputTokens = 1024
            temperature = 0.7
        }
    } | ConvertTo-Json -Depth 10

    $headers = @{
        "content-type" = "application/json"
    }

    $uri = "https://generativelanguage.googleapis.com/v1beta/models/$($Model):generateContent?key=$ApiKey"

    $response = Invoke-RestMethod -Uri $uri `
        -Method Post `
        -Headers $headers `
        -Body $body `
        -TimeoutSec 30

    return $response.candidates[0].content.parts[0].text.Trim()
}

# Function to call OpenAI API
function Invoke-OpenAIAPI {
    param(
        [string]$Prompt,
        [string]$ApiKey,
        [string]$Model
    )

    $body = @{
        model = $Model
        messages = @(
            @{
                role = "system"
                content = $systemPrompt
            }
            @{
                role = "user"
                content = $Prompt
            }
        )
        max_tokens = 1024
        temperature = 0.7
    } | ConvertTo-Json -Depth 10

    $headers = @{
        "Authorization" = "Bearer $ApiKey"
        "Content-Type" = "application/json"
    }

    $response = Invoke-RestMethod -Uri "https://api.openai.com/v1/chat/completions" `
        -Method Post `
        -Headers $headers `
        -Body $body `
        -TimeoutSec 30

    return $response.choices[0].message.content.Trim()
}

# Function to call appropriate AI provider
function Get-CommitMessageFromAI {
    param(
        [string]$Diff,
        [string]$Context,
        [string]$Feedback = ""
    )

    $userPrompt = if ($Feedback) {
        @"
Previous commit message didn't meet requirements. User feedback: $Feedback

Here's the git diff of staged changes:

``````
$Diff
``````

Recent commits for context:
$Context

Please generate an improved commit message based on the feedback.
"@
    } else {
        @"
Analyze the following git diff and generate a meaningful commit message.

Git diff of staged changes:

``````
$Diff
``````

Recent commits for context:
$Context

Generate a clear, professional commit message.
"@
    }

    try {
        switch ($config.provider) {
            "Claude" {
                return Invoke-ClaudeAPI -Prompt $userPrompt -ApiKey $config.apiKeys.claude -Model $config.models.claude
            }
            "Gemini" {
                return Invoke-GeminiAPI -Prompt $userPrompt -ApiKey $config.apiKeys.gemini -Model $config.models.gemini
            }
            "OpenAI" {
                return Invoke-OpenAIAPI -Prompt $userPrompt -ApiKey $config.apiKeys.openai -Model $config.models.openai
            }
        }
    }
    catch {
        Write-Host ""
        Write-Host "Error calling $($config.provider) API:" -ForegroundColor Red
        Write-Host $_.Exception.Message -ForegroundColor Red

        if ($_.Exception.Response) {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $responseBody = $reader.ReadToEnd()
            Write-Host $responseBody -ForegroundColor Red
        }

        Write-Host ""
        exit 1
    }
}

# Main interaction loop
$feedback = ""
$iteration = 0

while ($true) {
    $iteration++

    if ($iteration -eq 1) {
        Write-Host "Requesting commit message from $($config.provider)..." -ForegroundColor Yellow
    } else {
        Write-Host ""
        Write-Host "Requesting revised commit message from $($config.provider)..." -ForegroundColor Yellow
    }

    Write-Host ""

    $commitMessage = Get-CommitMessageFromAI -Diff $diff -Context $commitContext -Feedback $feedback

    Write-Host "======================================" -ForegroundColor Cyan
    Write-Host "  Suggested Commit Message" -ForegroundColor Cyan
    Write-Host "======================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host $commitMessage -ForegroundColor White
    Write-Host ""
    Write-Host "======================================" -ForegroundColor Cyan
    Write-Host ""

    Write-Host "Options:" -ForegroundColor Yellow
    Write-Host "  [y] Accept and commit" -ForegroundColor Green
    Write-Host "  [r] Request revision (provide feedback)" -ForegroundColor Yellow
    Write-Host "  [n] Cancel" -ForegroundColor Red
    Write-Host ""
    Write-Host "Choice [y/r/n]: " -NoNewline -ForegroundColor Cyan
    $choice = Read-Host

    if ($choice -eq 'y' -or $choice -eq 'Y') {
        # Commit with the message
        Write-Host ""
        Write-Host "Committing changes..." -ForegroundColor Yellow

        # Save commit message to temp file to handle multi-line messages
        $tempFile = [System.IO.Path]::GetTempFileName()
        $commitMessage | Out-File -FilePath $tempFile -Encoding UTF8

        git commit -F $tempFile

        Remove-Item $tempFile

        if ($LASTEXITCODE -eq 0) {
            Write-Host ""
            Write-Host "======================================" -ForegroundColor Green
            Write-Host "  Commit successful!" -ForegroundColor Green
            Write-Host "======================================" -ForegroundColor Green
            Write-Host ""

            # Show the commit
            git log -1 --pretty=format:"%h - %s%n%b" --color=always
            Write-Host ""
            Write-Host ""
        } else {
            Write-Host ""
            Write-Host "Error: Commit failed!" -ForegroundColor Red
            Write-Host ""
            exit 1
        }

        break
    }
    elseif ($choice -eq 'r' -or $choice -eq 'R') {
        Write-Host ""
        Write-Host "Please provide feedback for revision:" -ForegroundColor Yellow
        Write-Host "(e.g., 'make it shorter', 'add more detail about X', 'use different tone')" -ForegroundColor Gray
        Write-Host ""
        Write-Host "Feedback: " -NoNewline -ForegroundColor Cyan
        $feedback = Read-Host

        if (-not $feedback) {
            Write-Host ""
            Write-Host "No feedback provided. Please try again." -ForegroundColor Red
            $feedback = ""
            continue
        }
    }
    elseif ($choice -eq 'n' -or $choice -eq 'N') {
        Write-Host ""
        Write-Host "Commit cancelled. Changes remain staged." -ForegroundColor Yellow
        Write-Host ""
        exit 0
    }
    else {
        Write-Host ""
        Write-Host "Invalid choice. Please enter y, r, or n." -ForegroundColor Red
    }
}
