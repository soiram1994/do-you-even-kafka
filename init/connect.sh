#!/bin/zsh

# Define the Docker container name
CONNECT_CONTAINER_NAME="connect"

# Define the JDBC connector URL
JDBC_CONNECTOR_URL="https://path/to/confluentinc-kafka-connect-jdbc-<version>.zip"

# Download the JDBC connector
curl -o /tmp/kafka-connect-jdbc.zip $JDBC_CONNECTOR_URL

# Copy the JDBC connector to the Docker container
docker cp /tmp/kafka-connect-jdbc.zip $CONNECT_CONTAINER_NAME:/tmp/kafka-connect-jdbc.zip

# Install the JDBC connector in the Docker container
docker exec -it $CONNECT_CONTAINER_NAME bash -c "unzip /tmp/kafka-connect-jdbc.zip -d /usr/share/java/ && rm /tmp/kafka-connect-jdbc.zip"

# Restart the Docker container to apply changes
docker restart $CONNECT_CONTAINER_NAME

echo "JDBC connector installed successfully in the Docker Connect container."