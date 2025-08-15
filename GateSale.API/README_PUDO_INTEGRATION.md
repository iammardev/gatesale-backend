# PUDO Locker Integration

This document provides information on how to use and test the PUDO locker integration in the GateSale application.

## Available APIs

### Locker Discovery

- `GET /api/Locker/nearby?latitude={lat}&longitude={lon}&radius={radius}` - Find lockers near a location
- `GET /api/Locker/{lockerCode}` - Get details of a specific locker

### Order-Locker Operations

- `POST /api/Locker/assign/{orderId}` - Assign an order to a locker
- `POST /api/Locker/access-code/{orderId}` - Generate access code for pickup
- `POST /api/Locker/release` - Release a locker after use

### User Locker Preferences

- `GET /api/UserLocker/favorites` - Get user's favorite lockers
- `GET /api/UserLocker/default` - Get user's default locker
- `GET /api/UserLocker/seller/dropoff` - Get seller's preferred dropoff lockers
- `POST /api/UserLocker/favorites` - Add a locker to favorites
- `DELETE /api/UserLocker/favorites/{lockerCode}` - Remove from favorites
- `POST /api/UserLocker/default` - Set default pickup locker
- `POST /api/UserLocker/seller/dropoff` - Set preferred dropoff locker

### Order Tracking

- `GET /api/OrderTracking/{orderId}` - Get detailed order tracking info
- `GET /api/OrderTracking/{orderId}/locker-status` - Get locker status for an order
- `POST /api/OrderTracking/{orderId}/status` - Update order status
- `POST /api/OrderTracking/{orderId}/events` - Log custom order tracking events

### PUDO Webhooks

- `POST /api/PudoWebhook` - Generic webhook endpoint for all PUDO events
- `POST /api/PudoWebhook/status` - Webhook for locker status updates
- `POST /api/PudoWebhook/pickup` - Webhook for pickup confirmations

## Testing the APIs

### Prerequisites

1. Make sure the API is running: `dotnet run --project GateSaleBackend/GateSale.API/GateSale.API.csproj`
2. Get an authentication token using the test endpoint

### Using the HTTP File

The project includes a `.http` file that can be used with tools like Visual Studio Code (with the REST Client extension) or JetBrains Rider to test the APIs:

1. Open `GateSale.API.http` in your editor
2. First create a test order and get its ID:
   ```
   POST http://localhost:5221/api/Test/create-test-order
   ```
3. Generate a test JWT token:
   ```
   GET http://localhost:5221/api/Test/generate-test-token
   ```
4. Replace `YOUR_AUTH_TOKEN_HERE` with the token from the response
5. Replace example IDs (like `00000000-0000-0000-0000-000000000001`) with the actual order ID from step 2
6. Click the "Send Request" link above each request to execute it

### Using Postman

Here are examples of how to test the APIs using Postman:

#### 1. Create a Test Order

- **Method**: POST
- **URL**: `http://localhost:5221/api/Test/create-test-order`
- **Headers**: None required
- **Response**: 
  ```json
  {
    "orderId": "2627533d-f5ed-49e3-8eef-9d690170bddc",
    "orderNumber": "TEST-637986543210000000",
    "status": 0,
    "buyerId": "98765432-9876-9876-9876-987654321098"
  }
  ```

#### 2. Generate a Test JWT Token

- **Method**: GET
- **URL**: `http://localhost:5221/api/Test/generate-test-token`
- **Headers**: None required
- **Response**:
  ```json
  {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "expiresIn": 86400,
    "userId": "12345678-1234-1234-1234-123456789012",
    "tokenType": "Bearer"
  }
  ```

#### 3. Get Nearby Lockers

- **Method**: GET
- **URL**: `http://localhost:5221/api/Locker/nearby?latitude=-26.050789&longitude=28.023501&radius=10`
- **Headers**: None required

#### 4. Get Specific Locker

- **Method**: GET
- **URL**: `http://localhost:5221/api/Locker/MOCK001`
- **Headers**: None required

#### 5. Assign Order to Locker

- **Method**: POST
- **URL**: `http://localhost:5221/api/Locker/assign/{orderId}` (replace `{orderId}` with actual ID)
- **Headers**: 
  - Authorization: Bearer {your-token}
- **Body** (raw JSON):
  ```json
  {
    "lockerCode": "MOCK001"
  }
  ```

#### 6. Generate Access Code

- **Method**: POST
- **URL**: `http://localhost:5221/api/Locker/access-code/{orderId}` (replace `{orderId}` with actual ID)
- **Headers**: 
  - Authorization: Bearer {your-token}
- **Body** (raw JSON):
  ```json
  {
    "lockerCode": "MOCK001"
  }
  ```

#### 7. Release Locker

- **Method**: POST
- **URL**: `http://localhost:5221/api/Locker/release`
- **Headers**: 
  - Authorization: Bearer {your-token}
- **Body** (raw JSON):
  ```json
  {
    "lockerCode": "MOCK001",
    "accessCode": "123456"
  }
  ```

#### 8. User Locker Preferences

- **Method**: POST
- **URL**: `http://localhost:5221/api/UserLocker/favorites`
- **Headers**: 
  - Authorization: Bearer {your-token}
- **Body** (raw JSON):
  ```json
  {
    "lockerCode": "MOCK001"
  }
  ```

#### 9. Order Tracking

- **Method**: GET
- **URL**: `http://localhost:5221/api/OrderTracking/{orderId}` (replace `{orderId}` with actual ID)
- **Headers**: 
  - Authorization: Bearer {your-token}

### Testing Webhooks

#### 10. Simulate Locker Status Webhook

- **Method**: POST
- **URL**: `http://localhost:5221/api/PudoWebhook/status`
- **Headers**: 
  - Content-Type: application/json
- **Body** (raw JSON):
  ```json
  {
    "lockerCode": "MOCK001",
    "status": "Occupied",
    "transactionId": "tx_12345"
  }
  ```

#### 11. Simulate Pickup Confirmation Webhook

- **Method**: POST
- **URL**: `http://localhost:5221/api/PudoWebhook/pickup`
- **Headers**: 
  - Content-Type: application/json
- **Body** (raw JSON):
  ```json
  {
    "orderId": "2627533d-f5ed-49e3-8eef-9d690170bddc",
    "lockerCode": "MOCK001",
    "pickupTime": "2023-08-15T14:30:00Z"
  }
  ```

#### 12. Simulate Generic Webhook

- **Method**: POST
- **URL**: `http://localhost:5221/api/PudoWebhook`
- **Headers**: 
  - Content-Type: application/json
  - X-Pudo-Signature: YOUR_SIGNATURE_HERE
- **Body** (raw JSON):
  ```json
  {
    "eventType": "package_dropped",
    "lockerCode": "MOCK001",
    "orderReference": "ORDER123",
    "transactionId": "tx_12345",
    "timestamp": "2023-08-15T14:00:00Z"
  }
  ```

#### Using ngrok for External Webhook Testing

To test webhooks from external services:

1. Use a tool like [ngrok](https://ngrok.com/) to expose your local API to the internet:
   ```
   ngrok http 5221
   ```
2. Update the webhook URL in the PUDO dashboard to point to your ngrok URL
3. Alternatively, use the webhook simulation endpoints in the `.http` file

## Order Flow with PUDO Integration

1. **Buyer Flow**:
   - Buyer searches for nearby lockers during checkout (`GET /api/Locker/nearby`)
   - Selects preferred pickup locker (`POST /api/UserLocker/favorites` or `/default`)
   - Receives notification when order is ready for pickup (via webhook)
   - Gets access code to open locker (via app)
   - Picks up order using access code

2. **Seller Flow**:
   - Seller prepares order for delivery
   - Selects dropoff locker (`POST /api/UserLocker/seller/dropoff`)
   - Drops off package at locker using generated access code (`POST /api/Locker/access-code/{orderId}`)
   - Updates order status (`POST /api/OrderTracking/{orderId}/status`)
   - Receives confirmation when buyer picks up the order (via webhook)

3. **Order Tracking Flow**:
   - Order created (Pending)
   - Payment confirmed (Confirmed)
   - Seller drops off at locker (InTransit)
   - Locker system confirms dropoff (ReadyForPickup)
   - Buyer picks up package (Completed)
   - System updates locker status to Available

## Troubleshooting

- If you get authentication errors, make sure your JWT token is valid and not expired
- For webhook testing issues, check the logs for detailed error messages
- The system includes a sandbox mode that returns mock data when the real PUDO API is not available
- Webhook events are logged in the database for debugging purposes

### Common Issues and Solutions

1. **"relation 'OrderTrackingEvents' does not exist" error**
   - You need to create and apply a database migration:
   ```bash
   cd GateSaleBackend
   dotnet ef migrations add AddPudoLockerIntegrationTables --project GateSale.Infrastructure --startup-project GateSale.API
   dotnet ef database update --project GateSale.Infrastructure --startup-project GateSale.API
   ```

2. **"Failed to generate access code for locker" error**
   - Check that `UseSandbox` is set to `true` in your `appsettings.json` under the Pudo section
   - Make sure the order is properly assigned to a locker first using the assign endpoint

3. **"Unauthorized" error**
   - Make sure you're including the Authorization header with "Bearer " prefix
   - Generate a new token if yours has expired
   - Example header: `Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...`

4. **Testing Flow**
   For a complete test flow, follow these steps in order:
   1. Create a test order
   2. Generate a test token
   3. Assign the order to a locker
   4. Generate an access code
   5. Simulate package drop-off with webhook
   6. Check order status
   7. Simulate package pickup with webhook
   8. Verify order is completed

## Database Schema

The integration uses the following database tables:
- `Lockers` - Stores locker information
- `UserLockers` - Stores user preferences for lockers
- `OrderTrackingEvents` - Stores detailed tracking events for orders
- `PudoWebhookLogs` - Logs all incoming webhook events

## Configuration

PUDO integration settings are configured in `appsettings.json` under the `Pudo` section:

```json
"Pudo": {
  "ApiBaseUrl": "https://api.pudo.com/v1/",
  "ApiKey": "your_api_key",
  "WebhookSecret": "your_webhook_secret",
  "ConnectionTimeoutSeconds": 30,
  "UseSandbox": true
}
```

Set `UseSandbox` to `false` when connecting to the real PUDO API.

## Development Authentication

For development and testing purposes, you can use the test endpoints to:

1. Create test orders:
   ```
   POST /api/Test/create-test-order
   ```

2. Generate test JWT tokens:
   ```
   GET /api/Test/generate-test-token
   ```

These endpoints are only available in development mode and should not be deployed to production.