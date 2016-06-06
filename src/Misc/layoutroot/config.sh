#!/bin/bash
user_id=`id -u`

if [ $user_id -eq 0 ]; then
    echo "Must not run with sudo"
    exit 1
fi

# Ensure the permissions script can be executed by sudo.
chmod +rx ./bin/permissions.sh

# Export the env variables.
source ./env.sh

if [[ "$1" == "remove" ]]; then
    # Ensure permissions then defer to Agent.Listener to unconfigure.
    sudo ( ./bin/permissions.sh ; ./bin/Agent.Listener unconfigure )
else
    # Ensure permissions then defer to Agent.Listener to configure.
    sudo ( ./bin/permissions.sh ; ./bin/Agent.Listener configure $* )
fi
