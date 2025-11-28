namespace SchoolBookPlatform.DTOs;

public class ServiceResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; set; }
}