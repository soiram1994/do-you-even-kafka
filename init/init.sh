#!/bin/zsh

# Start Docker Compose services
docker compose up -d

# Wait for SQL Server to start by checking the connection
until docker exec -it sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost,1433 -U sa -P P@ssw0rd! -Q "SELECT 1" &> /dev/null
do
  echo "Waiting for SQL Server to start..."
  sleep 2
done

# Check if the database already exists
DB_EXISTS=$(docker exec -it sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost,1433 -U sa -P P@ssw0rd! -Q "IF DB_ID('order_db') IS NOT NULL SELECT 1 ELSE SELECT 0" -h -1)
if [ "$DB_EXISTS" -eq 1 ]; then
  echo "Database 'order_db' already exists."
else
  # Create the database
  docker exec -it sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost,1433 -U sa -P P@ssw0rd! -Q "CREATE DATABASE order_db;"

  # Run the SQL commands to create tables and insert data
  docker exec -it sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost,1433 -U sa -P P@ssw0rd! -d order_db -Q "
  CREATE TABLE customers (
      id INT IDENTITY(1,1) PRIMARY KEY,
      name VARCHAR(100) NOT NULL,
      email VARCHAR(100) NOT NULL UNIQUE
  );

  CREATE TABLE products (
      id INT IDENTITY(1,1) PRIMARY KEY,
      name VARCHAR(100) NOT NULL,
      price DECIMAL(10, 2) NOT NULL
  );

  CREATE TABLE orders (
      id INT IDENTITY(1,1) PRIMARY KEY,
      customer_id INT,
      order_date DATE,
      FOREIGN KEY (customer_id) REFERENCES customers(id)
  );

  CREATE TABLE order_items (
      id INT IDENTITY(1,1) PRIMARY KEY,
      order_id INT,
      product_id INT,
      quantity INT,
      FOREIGN KEY (order_id) REFERENCES orders(id),
      FOREIGN KEY (product_id) REFERENCES products(id)
  );

  INSERT INTO customers (name, email) VALUES ('John Doe', 'john.doe@example.com');
  INSERT INTO customers (name, email) VALUES ('Jane Smith', 'jane.smith@example.com');

  INSERT INTO products (name, price) VALUES ('Widget', 19.99);
  INSERT INTO products (name, price) VALUES ('Gadget', 29.99);

  INSERT INTO orders (customer_id, order_date) VALUES (1, '2024-11-30');
  INSERT INTO orders (customer_id, order_date) VALUES (2, '2024-11-30');

  INSERT INTO order_items (order_id, product_id, quantity) VALUES (1, 1, 2);
  INSERT INTO order_items (order_id, product_id, quantity) VALUES (1, 2, 1);
  INSERT INTO order_items (order_id, product_id, quantity) VALUES (2, 1, 1);
  "
  echo "Database 'order_db' created and populated."
fi

# Check if the JDBC connector already exists
CONNECTOR_EXISTS=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:8083/connectors/jdbc-source-connector)
if [ "$CONNECTOR_EXISTS" -eq 200 ]; then
  echo "JDBC connector 'jdbc-source-connector' already exists."
else

  # Create the JDBC connector
  curl -X POST -H "Content-Type: application/json" --data @jdbc-connector.json http://localhost:8083/connectors
fi

# Delete the /tmp folder
rm -rf /tmp

echo "Docker Compose services are up, SQL commands executed, JDBC connector checked/created, and /tmp folder deleted successfully."