using System.Reflection;
using System.Text.RegularExpressions;
using Ganss.Xss;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace SchoolBookPlatform.Filters;

public class AntiXssFilter : IAsyncActionFilter
{
    private readonly HtmlSanitizer _sanitizer;
    private readonly ILogger<AntiXssFilter> _logger;
    
    private readonly HashSet<string> _skipProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "Password", "PasswordHash", "Token", "RefreshToken", 
        "AccessToken", "ApiKey", "Secret", "PrivateKey",
        // Chat encryption fields
        "CipherText", "EncryptedPin", "EncryptedUrl", "EncryptedKey",
        "PrivateKeyEncrypted", "PublicKey", "PinCodeHash", "PinExchange"
    };
    
    //Regex phát hiện các pattern XSS nguy hiểm
    private static readonly Regex[] DangerousPatterns = 
    {
        new(@"<script[^>]*>[\s\S]*?</script>", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"javascript:", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"on\w+\s*=", RegexOptions.IgnoreCase | RegexOptions.Compiled), // onclick, onerror...
        new(@"<iframe[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"<embed[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"<object[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"eval\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"expression\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled) // CSS expression
    };

    public AntiXssFilter(HtmlSanitizer sanitizer, ILogger<AntiXssFilter> logger)
    {
        _sanitizer = sanitizer;
        _logger = logger;
        ConfigureSanitizer();
    }
    
    private void ConfigureSanitizer()
    {
        // Chỉ cho phép các thẻ HTML an toàn
        _sanitizer.AllowedTags.Clear();
        _sanitizer.AllowedTags.UnionWith(new[] 
        { 
            "p", "br", "strong", "em", "u", "h1", "h2", "h3", 
            "ul", "ol", "li", "a", "img", "blockquote", "code", "pre"
        });
        
        // Chỉ cho phép các thuộc tính an toàn
        _sanitizer.AllowedAttributes.Clear();
        _sanitizer.AllowedAttributes.UnionWith(new[] 
        { 
            "href", "src", "alt", "title", "class" 
        });
        
        // Chỉ cho phép scheme an toàn cho links
        _sanitizer.AllowedSchemes.Clear();
        _sanitizer.AllowedSchemes.UnionWith(new[] { "http", "https", "mailto" });
        
        // Bảo vệ khỏi DOM clobbering
        _sanitizer.AllowedCssProperties.Clear();
        
        _sanitizer.KeepChildNodes = false; // Không giữ lại child nodes của thẻ bị loại bỏ
        _sanitizer.AllowDataAttributes = false; // Chặn data-* attributes
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var hasXssAttempt = false;
        
        foreach (var argument in context.ActionArguments)
        {
            var value = argument.Value;
            if (value == null) continue;

            //tham số chuỗi
            if (value is string stringValue)
            {
                var (sanitized, isXss) = SanitizeString(stringValue, argument.Key);
                context.ActionArguments[argument.Key] = sanitized;
                hasXssAttempt |= isXss;
            }
            //tham số Object (Model/DTO)
            else if (value.GetType().IsClass && !value.GetType().IsArray)
            {
                hasXssAttempt |= SanitizeObject(value);
            }
            //Collection (List, Array)
            else if (value is System.Collections.IEnumerable enumerable && 
                     value.GetType() != typeof(string))
            {
                hasXssAttempt |= SanitizeCollection(enumerable);
            }
        }
        
        if (hasXssAttempt)
        {
            _logger.LogWarning(
                "XSS attempt detected! Controller: {Controller}, Action: {Action}, IP: {IP}",
                context.Controller.GetType().Name,
                context.ActionDescriptor.DisplayName,
                context.HttpContext.Connection.RemoteIpAddress
            );
        }

        await next();
    }
    
    private (string sanitized, bool isXss) SanitizeString(string input, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(input))
            return (input, false);
        
        if (_skipProperties.Contains(propertyName))
            return (input, false);

        var containsXss = DetectDangerousPatterns(input);
        
        var sanitized = _sanitizer.Sanitize(input);
        
        //Decode HTML entities để tránh bypass
        sanitized = System.Net.WebUtility.HtmlDecode(sanitized);
        sanitized = _sanitizer.Sanitize(sanitized); // Sanitize lần 2
        
        //Loại bỏ null bytes (có thể bypass một số filter)
        sanitized = sanitized.Replace("\0", string.Empty);
        
        //Trim whitespace
        sanitized = sanitized.Trim();

        return (sanitized, containsXss || input != sanitized);
    }
    
    private bool SanitizeObject(object obj)
    {
        var hasXss = false;
        var properties = obj.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.PropertyType == typeof(string) && p.CanRead && p.CanWrite);

        foreach (var property in properties)
        {
            if (_skipProperties.Contains(property.Name))
                continue;

            var rawValue = property.GetValue(obj) as string;
            if (!string.IsNullOrEmpty(rawValue))
            {
                var (sanitized, isXss) = SanitizeString(rawValue, property.Name);
                property.SetValue(obj, sanitized);
                hasXss |= isXss;
            }
        }

        return hasXss;
    }
    
    private bool SanitizeCollection(System.Collections.IEnumerable enumerable)
    {
        var hasXss = false;
        
        foreach (var item in enumerable)
        {
            if (item == null) continue;
            
            if (item is string str)
            {
                var (_, isXss) = SanitizeString(str, "CollectionItem");
                hasXss |= isXss;
            }
            else if (item.GetType().IsClass)
            {
                hasXss |= SanitizeObject(item);
            }
        }

        return hasXss;
    }
    
    private bool DetectDangerousPatterns(string input)
    {
        foreach (var pattern in DangerousPatterns)
        {
            if (pattern.IsMatch(input))
            {
                _logger.LogWarning("Dangerous XSS pattern detected: {Pattern}", pattern.ToString());
                return true;
            }
        }
        return false;
    }
}