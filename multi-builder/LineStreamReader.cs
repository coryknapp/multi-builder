using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

internal class LineStreamReader
{
    private readonly StringBuilder buffer = new StringBuilder();

    public event Action<string>? LineReceived;

    public void Add(string? data)
    {
        if (string.IsNullOrEmpty(data))
            return;

        buffer.Append(data);

        string bufStr = buffer.ToString();
        int newlineIdx;
        while ((newlineIdx = bufStr.IndexOf('\n')) != -1)
        {
            // Extract the line, trim any trailing \r
            string line = bufStr.Substring(0, newlineIdx).TrimEnd('\r');
            LineReceived?.Invoke(line);

            // Remove processed line from buffer
            bufStr = bufStr.Substring(newlineIdx + 1);
        }

        // Update buffer with any remaining partial line
        buffer.Clear();
        buffer.Append(bufStr);
    }
}
