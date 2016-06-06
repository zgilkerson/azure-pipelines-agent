#!/bin/bash

# Assume the working directory is the layout root.

# Ensure the execute bit is set for Agent.Listener.
if [ ! -x ./bin/Agent.Listener ]; then
    echo chmod +x ./bin/Agent.Listener
    chmod +x ./bin/Agent.Listener
fi

# Ensure the execute bit is set for Agent.Worker.
if [ ! -x ./bin/Agent.Worker ]; then
    echo chmod +x ./bin/Agent.Worker
    chmod +x ./bin/Agent.Worker
fi
