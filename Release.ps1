param(
    [Parameter(Mandatory=$true)]
    [string]$Version
)

$ErrorActionPreference = "Stop"

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "  Rivulet Release Script" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Validate version format (basic check for semver)
if ($Version -notmatch '^(\d+)\.(\d+)\.(\d+)(-[a-zA-Z0-9.]+)?$') {
    Write-Host "Error: Invalid version format. Expected format: X.Y.Z or X.Y.Z-prerelease" -ForegroundColor Red
    Write-Host "Examples: 1.0.0, 1.2.3-alpha, 2.0.0-beta.1" -ForegroundColor Yellow
    exit 1
}

# Extract major.minor from version
$Major = $Matches[1]
$Minor = $Matches[2]
$Patch = $Matches[3]
$Prerelease = $Matches[4]

# Branch name uses major.minor.x pattern (e.g., release/1.0.x)
$BranchName = "release/$Major.$Minor.x"
$TagName = "v$Version"

Write-Host "Version:        $Version" -ForegroundColor Green
Write-Host "Branch Pattern: $BranchName (all $Major.$Minor.* patches)" -ForegroundColor Green
Write-Host "Tag:            $TagName" -ForegroundColor Green
Write-Host ""

# Check for uncommitted changes
Write-Host "Checking for uncommitted changes..." -ForegroundColor Yellow
$status = git status --porcelain
if ($status) {
    Write-Host "Error: You have uncommitted changes. Please commit or stash them first." -ForegroundColor Red
    Write-Host ""
    git status --short
    exit 1
}

# Fetch latest from remote
Write-Host "Fetching latest changes from remote..." -ForegroundColor Yellow
git fetch origin

# Check if branch already exists locally
$branchExists = git branch --list $BranchName
if ($branchExists) {
    Write-Host "Branch '$BranchName' already exists locally. Switching to it..." -ForegroundColor Yellow
    git checkout $BranchName
} else {
    # Check if branch exists on remote
    $remoteBranchExists = git branch -r --list "origin/$BranchName"
    if ($remoteBranchExists) {
        Write-Host "Branch '$BranchName' exists on remote. Checking it out..." -ForegroundColor Yellow
        git checkout -b $BranchName origin/$BranchName
    } else {
        Write-Host "Creating new branch '$BranchName' from current branch..." -ForegroundColor Yellow
        git checkout -b $BranchName
    }
}

# Check if tag already exists
$tagExists = git tag --list $TagName
if ($tagExists) {
    Write-Host "Error: Tag '$TagName' already exists!" -ForegroundColor Red
    Write-Host "If you want to re-release, delete the tag first:" -ForegroundColor Yellow
    Write-Host "  git tag -d $TagName" -ForegroundColor Yellow
    Write-Host "  git push origin :refs/tags/$TagName" -ForegroundColor Yellow
    exit 1
}

# Get current commit information
Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "  Release Information" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

$commitHash = git rev-parse --short HEAD
$commitHashFull = git rev-parse HEAD
$commitAuthor = git log -1 --format='%an <%ae>'
$commitDate = git log -1 --format='%ci'
$commitMessage = git log -1 --format='%s'
$commitBody = git log -1 --format='%b'
$currentBranch = git branch --show-current
$remoteUrl = git config --get remote.origin.url

Write-Host "Version:        " -NoNewline -ForegroundColor Yellow
Write-Host "$Version" -ForegroundColor White

Write-Host "Tag:            " -NoNewline -ForegroundColor Yellow
Write-Host "$TagName" -ForegroundColor White

Write-Host "Branch:         " -NoNewline -ForegroundColor Yellow
Write-Host "$BranchName" -ForegroundColor White

Write-Host "Current Branch: " -NoNewline -ForegroundColor Yellow
Write-Host "$currentBranch" -ForegroundColor White

Write-Host ""
Write-Host "Commit Hash:    " -NoNewline -ForegroundColor Yellow
Write-Host "$commitHash ($commitHashFull)" -ForegroundColor Gray

Write-Host "Commit Author:  " -NoNewline -ForegroundColor Yellow
Write-Host "$commitAuthor" -ForegroundColor White

Write-Host "Commit Date:    " -NoNewline -ForegroundColor Yellow
Write-Host "$commitDate" -ForegroundColor White

Write-Host ""
Write-Host "Commit Message:" -ForegroundColor Yellow
Write-Host "  $commitMessage" -ForegroundColor White

if ($commitBody.Trim()) {
    Write-Host ""
    Write-Host "Commit Body:" -ForegroundColor Yellow
    $commitBody -split "`n" | ForEach-Object {
        Write-Host "  $_" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "Repository:     " -NoNewline -ForegroundColor Yellow
Write-Host "$remoteUrl" -ForegroundColor White

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "This will:" -ForegroundColor Yellow
Write-Host "  1. Create tag '$TagName' on commit $commitHash" -ForegroundColor Gray
Write-Host "  2. Push branch '$BranchName' to origin" -ForegroundColor Gray
Write-Host "  3. Push tag '$TagName' to origin" -ForegroundColor Gray
Write-Host "  4. Trigger GitHub Actions release workflow" -ForegroundColor Gray
Write-Host "  5. Publish NuGet package to nuget.org" -ForegroundColor Gray
Write-Host ""

# Ask for confirmation
Write-Host "Do you want to proceed with the release? " -NoNewline -ForegroundColor Yellow
Write-Host "[y/N]: " -NoNewline -ForegroundColor Cyan
$confirmation = Read-Host

if ($confirmation -ne 'y' -and $confirmation -ne 'Y') {
    Write-Host ""
    Write-Host "Release cancelled by user." -ForegroundColor Yellow
    Write-Host ""
    exit 0
}

# Create tag
Write-Host ""
Write-Host "Creating tag '$TagName'..." -ForegroundColor Yellow
git tag -a $TagName -m "Release $Version"

# Push branch
Write-Host "Pushing branch '$BranchName' to origin..." -ForegroundColor Yellow
git push -u origin $BranchName

# Push tag
Write-Host "Pushing tag '$TagName' to origin..." -ForegroundColor Yellow
git push origin $TagName

Write-Host ""
Write-Host "======================================" -ForegroundColor Green
Write-Host "  Release process completed!" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green
Write-Host ""
Write-Host "Branch '$BranchName' and tag '$TagName' have been pushed to origin." -ForegroundColor Green
Write-Host "The GitHub Actions workflow should now build and publish the package." -ForegroundColor Green
Write-Host ""
Write-Host "Monitor the workflow at:" -ForegroundColor Yellow
Write-Host "  https://github.com/Jeffeek/Rivulet/actions" -ForegroundColor Cyan
Write-Host ""
