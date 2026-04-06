# ACE Web Portal: Linux Infrastructure Setup Guide

This guide provides step-by-step instructions for performing the one-time infrastructure setup on a remote Linux server (Ubuntu/Debian recommended). This enables secure, high-performance production access to the ACE Web Portal.

---

## Part 1: Node.js & npm Installation
The Web Portal frontend requires Node.js to build. We recommend using the **NodeSource** distributions for stability on production servers.

### 1. Install Node.js LTS (v20+)
Execute these commands as an administrator (`sudo`):

```bash
# Download and import the NodeSource GPG key
sudo apt-get update
sudo apt-get install -y ca-certificates curl gnupg
sudo mkdir -p /etc/apt/keyrings
curl -fsSL https://deb.nodesource.com/gpgkey/nodesource-repo.gpg.key | sudo gpg --dearmor -o /etc/apt/keyrings/nodesource.gpg

# Create the repository entry (v20)
NODE_MAJOR=20
echo "deb [signed-by=/etc/apt/keyrings/nodesource.gpg] https://nodesource.com/node_$NODE_MAJOR.x nodistro main" | sudo tee /etc/apt/sources.list.d/nodesource.list

# Install Node.js and npm
sudo apt-get update
sudo apt-get install nodejs -y
```

### 2. Verify Installation
```bash
node -v  # Expected: v20.x.x
npm -v   # Expected: 10.x.x
```

---

## Part 2: Hardened NGINX Reverse Proxy
The ACE Web Portal binds internally to `localhost:5001`. We use **NGINX** to securely expose it to the public internet via Ports 80 and 443.

### 1. Install NGINX
```bash
sudo apt-get update
sudo apt-get install nginx -y
```

### 2. Production Configuration
Create a new configuration file:
`sudo nano /etc/nginx/sites-available/ace-portal`

Paste the following configuration (**Replace `<your-domain>` with your actual domain**):

```nginx
# 1. THE SECURE BLOCK (Port 443)
server {
    listen 443 ssl;
    server_name <your-domain>;

    # SSL Certificates (Provided by Certbot later)
    ssl_certificate /etc/letsencrypt/live/<your-domain>/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/<your-domain>/privkey.pem;
    include /etc/letsencrypt/options-ssl-nginx.conf;
    ssl_dhparam /etc/letsencrypt/ssl-dhparams.pem;

    # Security Headers
    add_header X-Frame-Options "SAMEORIGIN";
    add_header X-XSS-Protection "1; mode=block";
    add_header X-Content-Type-Options "nosniff";

    # Hardened Proxy Buffers (Prevents "400 Bad Request" for large cookies/JWTs)
    proxy_buffer_size 128k;
    proxy_buffers 4 256k;
    proxy_busy_buffers_size 256k;

    location / {
        # Proxy to the internal ACE Web Portal (Port 5001)
        proxy_pass http://127.0.0.1:5001;
        
        # Identity Headers (Required for Auth/Logging)
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;

        # SignalR & WebSocket Support (For real-time updates)
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_cache_bypass $http_upgrade;
    }
}

# 2. THE REDIRECT BLOCK (Implicit Port 80)
server {
    listen 80;
    server_name <your-domain>;
    return 301 https://$host$request_uri;
}
```

### 3. Enable the Site
```bash
# Enable the configuration
sudo ln -s /etc/nginx/sites-available/ace-portal /etc/nginx/sites-enabled/

# Remove the default site
sudo rm /etc/nginx/sites-enabled/default

# Test and Restart NGINX
sudo nginx -t
sudo systemctl restart nginx
```

---

## Part 3: Enable SSL (HTTPS)
Use **Certbot** to get a free SSL certificate from Let's Encrypt and automatically manage renewals.

### 1. Install Certbot
```bash
sudo apt-get install certbot python3-certbot-nginx -y
```

### 2. Generate Certificate
```bash
sudo certbot --nginx -d <your-domain>
```
> [!TIP]
> When prompted, select **Redirect** to ensure all traffic is encrypted. Certbot will automatically integrate with the configuration blocks we created above.

---

## Part 4: Initial Connectivity Verification (The 502 Test)
Before you deploy your ACE Server, we must verify that NGINX, SSL, and your firewall are all correctly configured.

### 1. The Verification Step
Visit your domain in a web browser: `https://<your-domain>`

### 2. Expected Result: **502 Bad Gateway**
If your configuration is correct, you **SHOULD** see a "502 Bad Gateway" error from NGINX. 

### 3. Why is this good?
- **502 Bad Gateway** means:
    - **Firewall (80/443)** is OPEN.
    - **DNS** is correctly pointing to your server.
    - **SSL (HTTPS)** is correctly terminated by NGINX.
    - **NGINX** is correctly attempting to forward the request to `127.0.0.1:5001`.
    - It is failing *only* because the ACE Server isn't running yet.

### Troubleshooting (If you don't see 502)
- **Timed Out**: Your firewall/UFW is likely blocking Port 443. 
- **Connection Refused**: NGINX is likely not running or not listening on 443.
- **SSL Error**: Your certificate paths in the NGINX config may be incorrect.

---

## Next Steps: Deployment
Now that your infrastructure is verified, proceed to the **[ACE Server Operations Guide](./readme_server_operations.md)** for instructions on performing your first build and deployment.
