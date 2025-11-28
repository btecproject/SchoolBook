# SchoolBook

## Project Setup

To run this project, create an `appsettings.json` file with the following content:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "..."
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Twilio": {
    "AccountSid": "....",
    "AuthToken": "....",
    "FromPhoneNumber": "...."
  },
  "SendGrid": {
    "ApiKey": "....",
    "FromEmail": "....",
    "FromName": "...."
  },
   "Cloudinary": {
    "CloudName": "....",
    "ApiKey": "....",
    "ApiSecret": "...."
  }
}
