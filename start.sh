#!/usr/bin/env bash
set -e

# ANSI color codes
BOLD='\033[1m'
GREEN='\033[0;32m'
CYAN='\033[0;36m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Determine script directory (repository root)
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$REPO_ROOT"

# Parse CLI arguments
MODE="dev"
for arg in "$@"; do
    case "$arg" in
        --single|--prod)
            MODE="single"
            ;;
        --api-only)
            MODE="api-only"
            ;;
        --help|-h)
            echo "GradCast Quick Start Script"
            echo ""
            echo "Usage: ./start.sh [options]"
            echo ""
            echo "Options:"
            echo "  (no args)         Start both API (port 5062) and Vite frontend (port 5173) with hot reload [Default]"
            echo "  --single, --prod  Build frontend into dist and run unified single-process ASP.NET server (port 5062)"
            echo "  --api-only        Run backend API only (port 5062)"
            echo "  -h, --help        Show this help message"
            exit 0
            ;;
        *)
            echo -e "${RED}Unknown option: $arg${NC}"
            echo "Run './start.sh --help' for available options."
            exit 1
            ;;
    esac
done

echo -e "${BOLD}${CYAN}═══════════════════════════════════════════════════${NC}"
echo -e "${BOLD}${CYAN}               GradCast Starter                    ${NC}"
echo -e "${BOLD}${CYAN}═══════════════════════════════════════════════════${NC}"
echo -e "Mode: ${BOLD}${GREEN}${MODE}${NC}"

# 1. Check .NET 10 SDK
if ! command -v dotnet >/dev/null 2>&1; then
    echo -e "${RED}Error: .NET SDK not found.${NC}"
    echo -e "Please install the .NET 10 SDK: ${CYAN}https://dotnet.microsoft.com/download${NC}"
    exit 1
fi

DOTNET_MAJOR=$(dotnet --version | cut -d. -f1)
if [ "$DOTNET_MAJOR" -lt 10 ]; then
    echo -e "${RED}Error: .NET 10 SDK or higher required. Current version: $(dotnet --version)${NC}"
    echo -e "Please install the .NET 10 SDK: ${CYAN}https://dotnet.microsoft.com/download${NC}"
    exit 1
fi
echo -e "✓ .NET SDK: ${GREEN}$(dotnet --version)${NC}"

# 2. Check Node.js and npm (if needed for frontend)
if [ "$MODE" != "api-only" ]; then
    if ! command -v node >/dev/null 2>&1; then
        echo -e "${RED}Error: Node.js not found.${NC}"
        echo -e "Please install Node.js 20+: ${CYAN}https://nodejs.org/${NC}"
        exit 1
    fi

    NODE_MAJOR=$(node -v | tr -d 'v' | cut -d. -f1)
    if [ "$NODE_MAJOR" -lt 20 ]; then
        echo -e "${RED}Error: Node.js 20 or higher required. Current version: $(node -v)${NC}"
        echo -e "Please install Node.js 20+: ${CYAN}https://nodejs.org/${NC}"
        exit 1
    fi
    echo -e "✓ Node.js:  ${GREEN}$(node -v)${NC}"

    if [ ! -d "src/web/node_modules" ]; then
        echo -e "${YELLOW}Installing web dependencies (npm install)...${NC}"
        npm --prefix src/web install
    fi
fi

# 3. Check for Port Conflicts
check_port() {
    local port=$1
    local name=$2
    if command -v lsof >/dev/null 2>&1; then
        local pid
        pid=$(lsof -ti :"$port" 2>/dev/null || true)
        if [ -n "$pid" ]; then
            echo -e "${RED}Error: Port $port ($name) is already in use by process PID $pid.${NC}"
            echo -e "To free it, run: ${BOLD}kill -9 $pid${NC}"
            exit 1
        fi
    fi
}

check_port 5062 "API"
if [ "$MODE" = "dev" ]; then
    check_port 5173 "Frontend Dev Server"
fi

# 4. Database Status Check
if [ ! -f "gradcast.db" ]; then
    echo -e "${YELLOW}Database (gradcast.db) not found.${NC}"
    echo -e "The API will automatically create and seed reference data (156 CBSAs, 156 FMRs) on startup."
fi

# 5. Process execution based on MODE
PIDS=()

cleanup() {
    if [ ${#PIDS[@]} -gt 0 ]; then
        echo ""
        echo -e "${YELLOW}Shutting down GradCast processes...${NC}"
        for pid in "${PIDS[@]}"; do
            if kill -0 "$pid" 2>/dev/null; then
                kill "$pid" 2>/dev/null || true
            fi
        done
        wait 2>/dev/null || true
        echo -e "${GREEN}All processes stopped.${NC}"
    fi
}

trap cleanup INT TERM EXIT

if [ "$MODE" = "single" ]; then
    echo -e "\n${CYAN}Building frontend assets for single-process serving...${NC}"
    npm --prefix src/web run build
    echo -e "${GREEN}Frontend build complete.${NC}\n"

    echo -e "${BOLD}${GREEN}Starting unified GradCast server...${NC}"
    echo -e "───────────────────────────────────────────────────"
    echo -e "  App & API:     ${BOLD}${CYAN}http://localhost:5062${NC}"
    echo -e "  Health Check:  ${CYAN}http://localhost:5062/health${NC}"
    echo -e "───────────────────────────────────────────────────"
    echo -e "Press ${BOLD}Ctrl+C${NC} to stop.\n"

    dotnet run --project src/api
elif [ "$MODE" = "api-only" ]; then
    echo -e "\n${BOLD}${GREEN}Starting GradCast API...${NC}"
    echo -e "───────────────────────────────────────────────────"
    echo -e "  API Root:      ${BOLD}${CYAN}http://localhost:5062${NC}"
    echo -e "  Health Check:  ${CYAN}http://localhost:5062/health${NC}"
    echo -e "───────────────────────────────────────────────────"
    echo -e "Press ${BOLD}Ctrl+C${NC} to stop.\n"

    dotnet run --project src/api
else
    # Dev mode: API + Vite Dev Server
    echo -e "\n${BOLD}${GREEN}Starting GradCast API and Frontend dev server...${NC}"

    dotnet run --project src/api &
    API_PID=$!
    PIDS+=("$API_PID")

    npm --prefix src/web run dev &
    WEB_PID=$!
    PIDS+=("$WEB_PID")

    echo -e "───────────────────────────────────────────────────"
    echo -e "  Frontend UI:   ${BOLD}${CYAN}http://localhost:5173${NC}"
    echo -e "  Backend API:   ${CYAN}http://localhost:5062${NC}"
    echo -e "  Health Check:  ${CYAN}http://localhost:5062/health${NC}"
    echo -e "───────────────────────────────────────────────────"
    echo -e "Press ${BOLD}Ctrl+C${NC} to stop both processes.\n"

    # Wait for child processes
    wait
fi
