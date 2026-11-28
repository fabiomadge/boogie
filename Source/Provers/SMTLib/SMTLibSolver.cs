using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Boogie.SMTLib;

public abstract class SMTLibSolver
{
  public const string PingRequest = "(get-info :name)";
  public abstract event Action<string> ErrorHandler;
  public abstract void Close();
  public abstract void Send(string cmd);
  public abstract Task<SExpr> SendRequest(string request,
    CancellationToken cancellationToken = default);

  public abstract Task<IReadOnlyList<SExpr>> SendRequestsAndCloseInput(IReadOnlyList<string> requests,
    CancellationToken cancellationToken = default);

  public abstract void NewProblem(string descriptiveName);

  public abstract Task PingPong(CancellationToken cancellationToken = default);

  public abstract void AddErrorHandler(Action<string> handler);

  public static bool IsPong(SExpr response)
  {
    return response is { Name: ":name" };
  }

  /// <summary>
  /// Asks whether the solver is still there, until the given token gives up on it. A solver that has stopped
  /// answering without closing either pipe is the case the token has to cover: nothing will arrive to end the
  /// wait. Cancelling has to reach the read rather than stop waiting for it, or the abandoned read keeps the
  /// solver's lock and every later request queues behind it.
  /// </summary>
  public async Task<ProverDiedException> GetExceptionIfProverDied(CancellationToken cancellationToken = default)
  {
    try
    {
      await PingPong(cancellationToken);
      return null;
    }
    catch (ProverDiedException e)
    {
      return e;
    }
  }
}