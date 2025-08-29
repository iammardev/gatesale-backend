# AWS Resources Used in GateSaleBackend

This document provides an overview of all AWS services and resources currently used in the GateSaleBackend project.

## AWS Region
- **Primary Region**: af-south-1 (Cape Town)

## Authentication & User Management
### Amazon Cognito
- **Usage**: 
  - User authentication and registration
  - Email verification
  - Password reset functionality
  - JWT token issuance and validation

## Storage
### Amazon S3
- **Usage**:
  - Storage for product images uploaded by users
  - Managed through S3StorageService implementation

## Database
### Amazon RDS (PostgreSQL)
- **Usage**:
  - Primary application database for storing all application data
  - Connected via Entity Framework Core

## Email Services
### Amazon SES (Simple Email Service)
- **Usage**:
  - Configured but currently not actively used
  - Email service is currently using Gmail SMTP instead

## AWS SDK Packages Used
- **AWSSDK.Core**: v3.7.301.9
- **AWSSDK.CognitoIdentityProvider**: v3.7.301.9
- **AWSSDK.S3**: v3.7.304.1
- **AWSSDK.Extensions.NETCore.Setup**: v3.7.7
- **AWSSDK.SecurityToken**: v3.7.300.6
- **Amazon.Extensions.CognitoAuthentication**: v2.5.2
- **Amazon.AspNetCore.Identity.Cognito**: v3.0.0

## Planned AWS Services for Future Development
The following AWS services are being considered for future implementation in the GateSaleBackend project:

### Amazon Rekognition
- **Planned Usage**:
  - Content moderation for product images
  - Automatic detection of prohibited items

### AWS Lambda
- **Planned Usage**:
  - Aws rekognition is not available in south africa region we will use lambda to access it.
  - Webhook handlers for third-party integrations

### Amazon SNS (Simple Notification Service)
- **Planned Usage**:
  - Push notifications to mobile devices
