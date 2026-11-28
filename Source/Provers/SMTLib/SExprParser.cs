using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Boogie;

namespace SMTLib;

public class SExprParser
{
  private int linePos;
  private string currLine;
  private readonly AsyncQueue<string> sexpLines = new();

  public event Action<string> ErrorHandler;

  public bool EndOfInput { get; private set; }

  public void AddLine(string line)
  {
    sexpLines.Enqueue(line);
  }

  public void EndInput()
  {
    sexpLines.Enqueue(null);
  }

  async Task<string> ReadLine(CancellationToken cancellationToken)
  {
    if (EndOfInput)
    {
      return null;
    }

    var line = await sexpLines.Dequeue(cancellationToken);
    if (line == null)
    {
      EndOfInput = true;
    }

    return line;
  }
  
  async Task<char> SkipWs(CancellationToken cancellationToken)
  {
    while (true)
    {
      if (currLine == null)
      {
        currLine = await ReadLine(cancellationToken);
        if (currLine == null)
        {
          return '\0';
        }
      }


      while (linePos < currLine.Length && char.IsWhiteSpace(currLine[linePos]))
      {
        linePos++;
      }

      if (linePos < currLine.Length && currLine[linePos] != ';')
      {
        return currLine[linePos];
      }
      else
      {
        currLine = null;
        linePos = 0;
      }
    }
  }

  void Shift()
  {
    linePos++;
  }

  async Task<string> ParseId(CancellationToken cancellationToken)
  {
    var sb = new StringBuilder();

    var begin = await SkipWs(cancellationToken);

    var quoted = begin == '"' || begin == '|';
    if (quoted)
    {
      Shift();
    }

    while (true)
    {
      if (linePos >= currLine.Length)
      {
        if (quoted)
        {
          do
          {
            sb.Append("\n");
            currLine = await ReadLine(cancellationToken);
          } while (currLine == "");
          if (currLine == null)
          {
            break;
          }

          linePos = 0;
        }
        else
        {
          break;
        }
      }

      var c = currLine[linePos++];
      if (quoted && c == begin)
      {
        break;
      }

      if (!quoted && (char.IsWhiteSpace(c) || c == '(' || c == ')'))
      {
        linePos--;
        break;
      }

      if (quoted && c == '\\' && linePos < currLine.Length && currLine[linePos] == '"')
      {
        sb.Append('"');
        linePos++;
        continue;
      }

      sb.Append(c);
    }

    return sb.ToString();
  }

  void ParseError(string msg)
  {
    ErrorHandler?.Invoke(msg);
  }

  public async IAsyncEnumerable<SExpr> ParseSExprs(bool top, CancellationToken cancellationToken = default)
  {
    while (true)
    {
      var c = await SkipWs(cancellationToken);
      if (c == '\0')
      {
        break;
      }

      if (c == ')')
      {
        if (top)
        {
          ParseError("stray ')'");
          Shift();
        }

        break;
      }

      string id;

      if (c == '(')
      {
        Shift();
        c = await SkipWs(cancellationToken);
        if (c == '\0')
        {
          ParseError("expecting something after '('");
          break;
        }
        else if (c == '(')
        {
          id = "";
        }
        else
        {
          id = await ParseId(cancellationToken);
        }

        var args = await ParseSExprs(false, cancellationToken).ToListAsync();

        c = await SkipWs(cancellationToken);
        if (c == ')')
        {
          Shift();
        }
        else
        {
          ParseError("unclosed '(" + id + "'");
        }

        yield return new SExpr(id, args);
      }
      else
      {
        id = await ParseId(cancellationToken);
        yield return new SExpr(id);
      }

      if (top)
      {
        break;
      }
    }
  }
}