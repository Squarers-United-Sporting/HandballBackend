#!/usr/bin/env bash

set -uo pipefail
ulimit -n 100000

errors=0
build=1
revision=""
debug=1
echo "Starting the server!!"
sleep 2


START() {
    current_branch=$(git branch --show-current || echo "")
    if [[ -z "$current_branch" ]]; then
        echo "Not a git branch!"
        ERROR
        return
    fi
    if [[ $debug -eq 0 ]]; then
        git stash
        git checkout master
        git pull
    fi
    revision=$(git rev-parse master)
    SUCCESS
}

ERROR() {
    errors=$((errors + 1))
    if [[ $errors -eq 1 ]]; then
        echo "There was an error building/downloading the branch! Waiting 10 seconds and trying again"
        sleep 10
    elif [[ $errors -eq 2 ]]; then
        echo "There was an error building/downloading the branch! Waiting 60 seconds and trying again"
        sleep 60
    elif [[ $errors -eq 3 ]]; then
        echo "There was an error building/downloading the branch! Waiting 5 minutes and trying again"
        sleep 300
    else
        echo "The server has failed to start $errors times! Exiting"
        exit 1
    fi
    START
}

SUCCESS() {
    errors=0
    while true; do
            export GIT_REVISION="$revision"
            clear
            if [[ build -eq 1 ]]; then
                docker compose up --build --exit-code-from handball-backend
            else 
                docker compose up --build --exit-code-from handball-backend
            fi
            EXIT_CODE=$?
            build=0
    
            case $EXIT_CODE in
                0) echo "Server exited normally." ; exit 0 ;;
                1) echo "A server restart was requested!" ; sleep 1 ;;
                2) echo "A server rebuild was requested!" ; sleep 1 ; build=1 ;;
                3) echo "A server git update was requested!" ; sleep 1 ; START ;;
                *) echo "Server exited with code $EXIT_CODE" ; ERROR ;;
            esac
        done
}

START

