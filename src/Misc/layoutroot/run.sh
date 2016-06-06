#!/bin/bash

user_id=`id -u`

if [ $user_id -eq 0 ]; then
    echo "Must not run interactively with sudo"
    exit 1
fi

if [ ! -f .Agent ]; then
    echo "Must configure first. Run ./config.sh"
    exit 1
fi

# Ensure permission.
chmod +rx ./bin/permissions.sh
./bin/permissions.sh

# Run the agent.
./bin/Agent.Listener run $*