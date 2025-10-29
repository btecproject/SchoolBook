# SchoolBook

Add appsettings.json to run:
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
  }
}
