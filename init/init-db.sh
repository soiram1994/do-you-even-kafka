#!/bin/bash

# Wait for SQL Server to start
sleep 30s

# Run the SQL commands
/opt/mssql-tools/bin/sqlcmd -S localhost,1433 -U sa -P YourStrong!Passw0rd -Q "
CREATE DATABASE order_db;

USE order_db;

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