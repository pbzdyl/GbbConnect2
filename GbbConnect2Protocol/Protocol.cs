namespace GbbConnect2Protocol;

public class Header
{

    public Line[]? Lines { get; set; }

    /// <summary>
    /// Error: null -> no error
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Any string up to 256 characters returned in answer
    /// </summary>
    public string? OrderId { get; set; }

}

public class Line
{
    /// <summary>
    /// 1,2,3,4...
    /// </summary>
    public int LineNo { get; set; }
    /// <summary>
    /// Any string up to 256 characters returned in anwer
    /// </summary>
    public string? Tag { get; set; }
    /// <summary>
    /// unix timestamp in seconds UTC
    /// </summary>
    public long? Timestamp { get; set; } 
    /// <summary>
    /// modbus command or response
    /// </summary>
    public string? Modbus { get; set; }
    /// <summary>
    /// on responce: error or null
    /// </summary>
    public string? Error { get; set; }


}

