#!/bin/sh

interval_seconds=10
while true; do
     RUNNING=$(docker inspect --format '{{.State.Running}}' app 2>/dev/null || echo "false")

     case $RUNNING in
          "true")
               echo "container is good"
               ;;

          "false" | *)
               echo "Attempted restart at $(date)" >> ContainerHealth.log
               docker restart app
               sleep 10
               ;;
     esac

     sleep $interval_seconds
done