#!/bin/bash

# AI-Powered Commit Assistant - Bash Version
# Equivalent to SmartCommit.ps1

set -e

# Color codes
CYAN='\033[0;36m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
GRAY='\033[0;37m'
WHITE='\033[1;37m'
NC='\033[0m' # No Color

# Default values
PROVIDER="Auto"
API_KEY=""
CONFIG_FILE=".smartcommit.config.json"

# Parse command-line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --provider)
            PROVIDER="$2"
            shift 2
            ;;
        --api-key)
            API_KEY="$2"
            shift 2
            ;;
        --config)
            CONFIG_FILE="$2"
            shift 2
            ;;
        -h|--help)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --provider PROVIDER    AI provider (Claude, Gemini, OpenAI, Auto). Default: Auto"
            echo "  --api-key KEY         API key for the selected provider"
            echo "  --config FILE         Config file path. Default: .smartcommit.config.json"
            echo "  -h, --help            Show this help message"
            echo ""
            exit 0
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

# Validate provider
if [[ ! "$PROVIDER" =~ ^(Claude|Gemini|OpenAI|Auto)$ ]]; then
    echo -e "${RED}Error: Invalid provider '$PROVIDER'. Must be: Claude, Gemini, OpenAI, or Auto${NC}"
    exit 1
fi

echo -e "${CYAN}======================================${NC}"
echo -e "${CYAN}  AI-Powered Commit Assistant${NC}"
echo -e "${CYAN}======================================${NC}"
echo ""

# Check for required tools
if ! command -v jq &> /dev/null; then
    echo -e "${RED}Error: 'jq' is required but not installed.${NC}"
    echo -e "${YELLOW}Install with: sudo apt install jq (Ubuntu/Debian) or brew install jq (macOS)${NC}"
    echo ""
    exit 1
fi

if ! command -v curl &> /dev/null; then
    echo -e "${RED}Error: 'curl' is required but not installed.${NC}"
    exit 1
fi

# Initialize config with defaults
declare -A CONFIG_API_KEYS
declare -A CONFIG_MODELS

CONFIG_PROVIDER="Claude"
CONFIG_API_KEYS[claude]="${ANTHROPIC_API_KEY:-}"
CONFIG_API_KEYS[gemini]="${GOOGLE_API_KEY:-}"
CONFIG_API_KEYS[openai]="${OPENAI_API_KEY:-}"
CONFIG_MODELS[claude]="claude-3-5-sonnet-20241022"
CONFIG_MODELS[gemini]="gemini-2.0-flash-exp"
CONFIG_MODELS[openai]="gpt-4o"

# Load config file if exists
if [[ -f "$CONFIG_FILE" ]]; then
    echo -e "${GRAY}Loading configuration from $CONFIG_FILE...${NC}"

    # Read provider
    FILE_PROVIDER=$(jq -r '.provider // empty' "$CONFIG_FILE" 2>/dev/null || echo "")
    if [[ -n "$FILE_PROVIDER" ]]; then
        CONFIG_PROVIDER="$FILE_PROVIDER"
    fi

    # Read API keys
    FILE_CLAUDE_KEY=$(jq -r '.apiKeys.claude // empty' "$CONFIG_FILE" 2>/dev/null || echo "")
    FILE_GEMINI_KEY=$(jq -r '.apiKeys.gemini // empty' "$CONFIG_FILE" 2>/dev/null || echo "")
    FILE_OPENAI_KEY=$(jq -r '.apiKeys.openai // empty' "$CONFIG_FILE" 2>/dev/null || echo "")

    [[ -n "$FILE_CLAUDE_KEY" ]] && CONFIG_API_KEYS[claude]="$FILE_CLAUDE_KEY"
    [[ -n "$FILE_GEMINI_KEY" ]] && CONFIG_API_KEYS[gemini]="$FILE_GEMINI_KEY"
    [[ -n "$FILE_OPENAI_KEY" ]] && CONFIG_API_KEYS[openai]="$FILE_OPENAI_KEY"

    # Read models
    FILE_CLAUDE_MODEL=$(jq -r '.models.claude // empty' "$CONFIG_FILE" 2>/dev/null || echo "")
    FILE_GEMINI_MODEL=$(jq -r '.models.gemini // empty' "$CONFIG_FILE" 2>/dev/null || echo "")
    FILE_OPENAI_MODEL=$(jq -r '.models.openai // empty' "$CONFIG_FILE" 2>/dev/null || echo "")

    [[ -n "$FILE_CLAUDE_MODEL" ]] && CONFIG_MODELS[claude]="$FILE_CLAUDE_MODEL"
    [[ -n "$FILE_GEMINI_MODEL" ]] && CONFIG_MODELS[gemini]="$FILE_GEMINI_MODEL"
    [[ -n "$FILE_OPENAI_MODEL" ]] && CONFIG_MODELS[openai]="$FILE_OPENAI_MODEL"
fi

# Override with parameter if provided
if [[ "$PROVIDER" != "Auto" ]]; then
    CONFIG_PROVIDER="$PROVIDER"
fi

# Auto-detect provider if set to Auto
if [[ "$PROVIDER" == "Auto" ]]; then
    if [[ -n "${CONFIG_API_KEYS[claude]}" ]]; then
        CONFIG_PROVIDER="Claude"
    elif [[ -n "${CONFIG_API_KEYS[gemini]}" ]]; then
        CONFIG_PROVIDER="Gemini"
    elif [[ -n "${CONFIG_API_KEYS[openai]}" ]]; then
        CONFIG_PROVIDER="OpenAI"
    else
        echo -e "${RED}Error: No API keys found! Please set one of:${NC}"
        echo -e "${YELLOW}  - ANTHROPIC_API_KEY (for Claude)${NC}"
        echo -e "${YELLOW}  - GOOGLE_API_KEY (for Gemini)${NC}"
        echo -e "${YELLOW}  - OPENAI_API_KEY (for OpenAI)${NC}"
        echo ""
        echo -e "${YELLOW}Or create a config file: $CONFIG_FILE${NC}"
        echo ""
        exit 1
    fi
fi

# Override API key if provided as parameter
if [[ -n "$API_KEY" ]]; then
    case "$CONFIG_PROVIDER" in
        Claude)
            CONFIG_API_KEYS[claude]="$API_KEY"
            ;;
        Gemini)
            CONFIG_API_KEYS[gemini]="$API_KEY"
            ;;
        OpenAI)
            CONFIG_API_KEYS[openai]="$API_KEY"
            ;;
    esac
fi

# Validate API key for selected provider
PROVIDER_LOWER=$(echo "$CONFIG_PROVIDER" | tr '[:upper:]' '[:lower:]')
CURRENT_API_KEY="${CONFIG_API_KEYS[$PROVIDER_LOWER]}"

if [[ -z "$CURRENT_API_KEY" ]]; then
    echo -e "${RED}Error: No API key found for provider '$CONFIG_PROVIDER'!${NC}"
    echo ""
    echo -e "${YELLOW}Set the appropriate environment variable:${NC}"
    case "$CONFIG_PROVIDER" in
        Claude)
            echo -e "${GRAY}  export ANTHROPIC_API_KEY='your-api-key'${NC}"
            echo -e "${CYAN}  Get key at: https://console.anthropic.com/${NC}"
            ;;
        Gemini)
            echo -e "${GRAY}  export GOOGLE_API_KEY='your-api-key'${NC}"
            echo -e "${CYAN}  Get key at: https://aistudio.google.com/apikey${NC}"
            ;;
        OpenAI)
            echo -e "${GRAY}  export OPENAI_API_KEY='your-api-key'${NC}"
            echo -e "${CYAN}  Get key at: https://platform.openai.com/api-keys${NC}"
            ;;
    esac
    echo ""
    exit 1
fi

echo -e "${GREEN}Provider: $CONFIG_PROVIDER${NC}"
echo -e "${GREEN}Model:    ${CONFIG_MODELS[$PROVIDER_LOWER]}${NC}"
echo ""

# Navigate to repository root (2 levels up from scripts/SmartCommit/)
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR/../.."

# Check for uncommitted changes
echo -e "${YELLOW}Checking for changes...${NC}"
STATUS=$(git status --porcelain)
if [[ -z "$STATUS" ]]; then
    echo -e "${GREEN}No changes to commit. Working tree is clean.${NC}"
    echo ""
    exit 0
fi

# Show current status
echo ""
echo -e "${YELLOW}Current changes:${NC}"
git status --short
echo ""

# Check if there are staged changes
STAGED_CHANGES=$(git diff --cached --name-only)
UNSTAGED_CHANGES=$(git diff --name-only)

if [[ -z "$STAGED_CHANGES" ]] && [[ -n "$UNSTAGED_CHANGES" ]]; then
    echo -e "${YELLOW}No staged changes found. Stage changes first?${NC}"
    echo -e "${GRAY}  [a] Stage all changes (git add .)${NC}"
    echo -e "${GRAY}  [s] Skip and exit (stage manually)${NC}"
    echo ""
    echo -ne "${CYAN}Choice [a/s]: ${NC}"
    read -r STAGE_CHOICE

    if [[ "$STAGE_CHOICE" =~ ^[aA]$ ]]; then
        echo ""
        echo -e "${YELLOW}Staging all changes...${NC}"
        git add .
    else
        echo ""
        echo -e "${YELLOW}Exiting. Please stage your changes manually with 'git add'.${NC}"
        echo ""
        exit 0
    fi
fi

# Get the diff of staged changes
echo ""
echo -e "${YELLOW}Analyzing changes...${NC}"
DIFF=$(git diff --cached)

if [[ -z "$DIFF" ]]; then
    echo -e "${RED}Error: No staged changes to commit.${NC}"
    echo ""
    exit 1
fi

# Get recent commit messages for context
RECENT_COMMITS=$(git log -5 --pretty=format:"%s" 2>/dev/null || echo "")

# System prompt for all providers
read -r -d '' SYSTEM_PROMPT << 'EOF' || true
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
EOF

# Function to call Claude API
call_claude_api() {
    local prompt="$1"
    local api_key="$2"
    local model="$3"

    local body=$(jq -n \
        --arg model "$model" \
        --arg system "$SYSTEM_PROMPT" \
        --arg content "$prompt" \
        '{
            model: $model,
            max_tokens: 1024,
            messages: [{
                role: "user",
                content: $content
            }],
            system: $system
        }')

    local response=$(curl -s -X POST https://api.anthropic.com/v1/messages \
        -H "x-api-key: $api_key" \
        -H "anthropic-version: 2023-06-01" \
        -H "content-type: application/json" \
        -d "$body" \
        --max-time 30)

    # Check for errors
    local error=$(echo "$response" | jq -r '.error.message // empty' 2>/dev/null)
    if [[ -n "$error" ]]; then
        echo -e "\n${RED}Error calling Claude API:${NC}"
        echo -e "${RED}$error${NC}\n"
        exit 1
    fi

    echo "$response" | jq -r '.content[0].text' | sed 's/^[[:space:]]*//;s/[[:space:]]*$//'
}

# Function to call Gemini API
call_gemini_api() {
    local prompt="$1"
    local api_key="$2"
    local model="$3"

    local combined_prompt="${SYSTEM_PROMPT}\n\n${prompt}"

    local body=$(jq -n \
        --arg text "$combined_prompt" \
        '{
            contents: [{
                parts: [{
                    text: $text
                }]
            }],
            generationConfig: {
                maxOutputTokens: 1024,
                temperature: 0.7
            }
        }')

    local response=$(curl -s -X POST \
        "https://generativelanguage.googleapis.com/v1beta/models/${model}:generateContent?key=${api_key}" \
        -H "content-type: application/json" \
        -d "$body" \
        --max-time 30)

    # Check for errors
    local error=$(echo "$response" | jq -r '.error.message // empty' 2>/dev/null)
    if [[ -n "$error" ]]; then
        echo -e "\n${RED}Error calling Gemini API:${NC}"
        echo -e "${RED}$error${NC}\n"
        exit 1
    fi

    echo "$response" | jq -r '.candidates[0].content.parts[0].text' | sed 's/^[[:space:]]*//;s/[[:space:]]*$//'
}

# Function to call OpenAI API
call_openai_api() {
    local prompt="$1"
    local api_key="$2"
    local model="$3"

    local body=$(jq -n \
        --arg model "$model" \
        --arg system "$SYSTEM_PROMPT" \
        --arg user "$prompt" \
        '{
            model: $model,
            messages: [
                {
                    role: "system",
                    content: $system
                },
                {
                    role: "user",
                    content: $user
                }
            ],
            max_tokens: 1024,
            temperature: 0.7
        }')

    local response=$(curl -s -X POST https://api.openai.com/v1/chat/completions \
        -H "Authorization: Bearer $api_key" \
        -H "Content-Type: application/json" \
        -d "$body" \
        --max-time 30)

    # Check for errors
    local error=$(echo "$response" | jq -r '.error.message // empty' 2>/dev/null)
    if [[ -n "$error" ]]; then
        echo -e "\n${RED}Error calling OpenAI API:${NC}"
        echo -e "${RED}$error${NC}\n"
        exit 1
    fi

    echo "$response" | jq -r '.choices[0].message.content' | sed 's/^[[:space:]]*//;s/[[:space:]]*$//'
}

# Function to get commit message from AI
get_commit_message_from_ai() {
    local diff_text="$1"
    local context="$2"
    local feedback="$3"

    local user_prompt
    if [[ -n "$feedback" ]]; then
        user_prompt="Previous commit message didn't meet requirements. User feedback: $feedback

Here's the git diff of staged changes:

\`\`\`
$diff_text
\`\`\`

Recent commits for context:
$context

Please generate an improved commit message based on the feedback."
    else
        user_prompt="Analyze the following git diff and generate a meaningful commit message.

Git diff of staged changes:

\`\`\`
$diff_text
\`\`\`

Recent commits for context:
$context

Generate a clear, professional commit message."
    fi

    case "$CONFIG_PROVIDER" in
        Claude)
            call_claude_api "$user_prompt" "${CONFIG_API_KEYS[claude]}" "${CONFIG_MODELS[claude]}"
            ;;
        Gemini)
            call_gemini_api "$user_prompt" "${CONFIG_API_KEYS[gemini]}" "${CONFIG_MODELS[gemini]}"
            ;;
        OpenAI)
            call_openai_api "$user_prompt" "${CONFIG_API_KEYS[openai]}" "${CONFIG_MODELS[openai]}"
            ;;
    esac
}

# Main interaction loop
FEEDBACK=""
ITERATION=0

while true; do
    ((ITERATION++))

    if [[ $ITERATION -eq 1 ]]; then
        echo -e "${YELLOW}Requesting commit message from $CONFIG_PROVIDER...${NC}"
    else
        echo ""
        echo -e "${YELLOW}Requesting revised commit message from $CONFIG_PROVIDER...${NC}"
    fi

    echo ""

    COMMIT_MESSAGE=$(get_commit_message_from_ai "$DIFF" "$RECENT_COMMITS" "$FEEDBACK")

    echo -e "${CYAN}======================================${NC}"
    echo -e "${CYAN}  Suggested Commit Message${NC}"
    echo -e "${CYAN}======================================${NC}"
    echo ""
    echo -e "${WHITE}${COMMIT_MESSAGE}${NC}"
    echo ""
    echo -e "${CYAN}======================================${NC}"
    echo ""

    echo -e "${YELLOW}Options:${NC}"
    echo -e "${GREEN}  [y] Accept and commit${NC}"
    echo -e "${YELLOW}  [r] Request revision (provide feedback)${NC}"
    echo -e "${RED}  [n] Cancel${NC}"
    echo ""
    echo -ne "${CYAN}Choice [y/r/n]: ${NC}"
    read -r CHOICE

    if [[ "$CHOICE" =~ ^[yY]$ ]]; then
        # Commit with the message
        echo ""
        echo -e "${YELLOW}Committing changes...${NC}"

        # Save commit message to temp file to handle multi-line messages
        TEMP_FILE=$(mktemp)
        echo "$COMMIT_MESSAGE" > "$TEMP_FILE"

        if git commit -F "$TEMP_FILE"; then
            rm -f "$TEMP_FILE"

            echo ""
            echo -e "${GREEN}======================================${NC}"
            echo -e "${GREEN}  Commit successful!${NC}"
            echo -e "${GREEN}======================================${NC}"
            echo ""

            # Show the commit
            git log -1 --pretty=format:"%h - %s%n%b" --color=always
            echo ""
            echo ""
        else
            rm -f "$TEMP_FILE"
            echo ""
            echo -e "${RED}Error: Commit failed!${NC}"
            echo ""
            exit 1
        fi

        break
    elif [[ "$CHOICE" =~ ^[rR]$ ]]; then
        echo ""
        echo -e "${YELLOW}Please provide feedback for revision:${NC}"
        echo -e "${GRAY}(e.g., 'make it shorter', 'add more detail about X', 'use different tone')${NC}"
        echo ""
        echo -ne "${CYAN}Feedback: ${NC}"
        read -r FEEDBACK

        if [[ -z "$FEEDBACK" ]]; then
            echo ""
            echo -e "${RED}No feedback provided. Please try again.${NC}"
            FEEDBACK=""
            continue
        fi
    elif [[ "$CHOICE" =~ ^[nN]$ ]]; then
        echo ""
        echo -e "${YELLOW}Commit cancelled. Changes remain staged.${NC}"
        echo ""
        exit 0
    else
        echo ""
        echo -e "${RED}Invalid choice. Please enter y, r, or n.${NC}"
    fi
done
