#!/usr/bin/env bash

set -uo pipefail

errors=0
echo "Starting the server!!"
sleep 2

START() {
    current_branch=$(git branch --show-current || echo "")
    if [[ -z "$current_branch" ]]; then
        echo "Not a git branch!"
        ERROR
        return
    fi

    git stash
    git checkout master
    git pull

    cd ./build/resources || exit 1
    git pull
    git add .
    git commit -m "Automatic Commit from Server Restart"
    git push origin
    cd ..
    cd ..

    BUILD
}

BUILD() {
    if ! dotnet clean ./HandballBackend/HandballBackend.csproj -c Release; then
        ERROR
        return
    fi

    if ! dotnet publish ./HandballBackend/HandballBackend.csproj -c Release \
        --runtime linux-x64 \
        --self-contained true \
        /p:PublishSingleFile=true \
        --framework net9.0 \
        --output ./build; then
        ERROR
        return
    fi

    git checkout "$current_branch"
    git stash pop || true

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
        echo "The file has failed to start $errors times! Exiting"
        exit 1
    fi
    START
}

SUCCESS() {
    errors=0
    cd ./build || exit 1
    while true; do
            clear
            ./HandballBackend -l false -u -b -s
            EXIT_CODE=$?
    
            case $EXIT_CODE in
                0) echo "Server exited normally." ; exit 0 ;;
                1) echo "A server restart was requested!" ; sleep 1 ;;
                2) echo "A server rebuild was requested!" ; sleep 1 ; cd .. ; BUILD ; return ;;
                3) echo "A server git update was requested!" ; sleep 1 ; cd .. ; START ; return ;;
                *) echo "Server exited with code $EXIT_CODE" ; exit $EXIT_CODE ;;
            esac
        done
}

START

