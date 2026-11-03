# PR #1170 — reviewed title, description and commit message

**Reviewed code:** `fork/pr1170-reviewed` @ `f70d8f5b` — one squashed commit on master `751228a2`.
The PR head `fix/abandoned-check-shares-solver` is still `b3256ecb`, the old nine-test version;
**do not diff against the PR head.** To land:

```
git fetch fork pr1170-reviewed
git push origin fork/pr1170-reviewed:fix/abandoned-check-shares-solver \
  --force-with-lease=fix/abandoned-check-shares-solver:b3256ecb
```

Verified at that commit: clean build, 567/567 unit tests, and `lit` introduces no failure (see
Testing below). Since boogie-org/boogie squash-merges, the title and description below become the
mainline commit; the branch commit message is transient.

## Title

fix: A cancelled or timed-out check shares its solver with the next one

## Description

`Checker.GoBackToIdle` gave a checker back to the pool while the check its caller had stopped waiting for was
still sending to the solver. The next check to take that checker shared the solver process, and the prover's
state in memory, with the abandoned one. Their commands interleave, and since `SMTLibProcess.Send` is not
synchronized they also tear, so the solver answers `invalid command, '(' expected`, or names symbols the check
never sent, such as `$generated@@26` (both in dafny-lang/dafny#2316 and dafny-lang/dafny#5798), or reports
`ControlFlow` as already declared (#1064). Either way the next check reports a wrong result, or waits forever.

A caller stops waiting when its check is cancelled or its time limit passes: `VerificationTask.StartRun` in a
language server, `SplitAndVerifyWorker.DoWork` on the command line. #932 opened the window by letting both stop
waiting for `Split.BeginCheck`. Dafny takes it for every check since it set a default time limit
(dafny-lang/dafny#6028), and the two flaky language server tests above are one of each trigger:
dafny-lang/dafny#2316 cancels a run, dafny-lang/dafny#5798 lets a time limit pass.

On the command line the time limit also crashed Boogie outright, because `DoWork` let the `TimeoutException`
escape. It takes building and sending a VC longer than the check's time limit, which for keep-going splits is a
second by default. Any program whose VC races that limit does it:

```boogie
procedure Unroll()
{
  var i: int;
  while (i < 10000) { i := i + 1; }
  assert i >= 0;
}
```

`/loopUnroll:2000 /timeLimit:1`, whenever the VC is not sent within the second → exit 134:

```
Unhandled exception. System.AggregateException: One or more errors occurred. (The operation has timed out.)
 ---> System.TimeoutException: The operation has timed out.
   at VC.SplitAndVerifyWorker.StartCheck(Int32 iteration, Split split, Checker checker, CancellationToken
      cancellationToken) in Source/VCGeneration/Splits/SplitAndVerifyWorker.cs:line 163
```

That is the crash reported in #1064, down to the line. With this change:

```
unroll.bpl(1,11): Verification of 'Unroll' timed out after 1 seconds

Boogie program verifier finished with 0 verified, 0 errors, 1 time out
```

**Fix.** `GoBackToIdle` takes the task of the check its caller began, and closes the checker instead of pooling
it while that check or its prover task still runs. It also closes the checker when the ping fails for any
reason; before, only a timeout did, and any other exception left the checker's slot in the pool taken.
`CheckerDied` ignores a disposed pool, as `AddChecker` does: releasing the disposed semaphore threw before the
checker could be closed, so its solver kept running. Dafny's language server takes that path whenever an edit
arrives during verification, because it disposes the engine of the document's previous version while that
version's checks are being cancelled. `DoWork` gives its checker back in one place, a `finally`, and before the
splits that a failed split is split into are verified: before, `ProcessResultAndReleaseChecker` gave it back and
`DoWork`'s `catch` gave it back again when one of those splits failed, so the pool could hold the same checker
twice. A check whose beginning outlasts its time limit is reported as timed out, or split further when
keep-going allows.

**Scope.** This does not lock `SMTLibProcess.Send`, which would stop the tearing but not the sharing. Closing a
checker instead of reusing it costs a solver start. The time-limit part needs the rest: with the in-flight check
removed, a check whose beginning outlasts its limit is still reported as timed out, but the next procedure runs
on the abandoned check's solver, with 15 overlapping sends, and is still reported verified.

**Testing.** Eight tests give a check a solver that counts the Sends overlapping on it, and block one Send to
hold the check open. They cover cancellation and a time limit on both paths, the prover-task phase, a broken
pipe, a failed keep-going split and a disposed engine. All eight fail on master: two on overlapping sends, one
because the pool hands the same checker to two checks, one on a broken pipe, one by aborting the test host on a
`Contract.Assert`, and three on an escaped `TimeoutException`. Reverting any one part of the fix fails at least
one of them. No `lit` failure is introduced: under `batch_mode=True` the failure set equals master's exactly,
and under `batch_mode=False` the two stable failures fail on master too, the rest being load-dependent here.

This and #1171 both make `GoBackToIdle` catch every exception from the ping, so they conflict in that hunk;
whichever lands second keeps this version.

## Branch commit message (transient — squash-merge discards it)

```
fix: A cancelled or timed-out check shares its solver with the next one

When a caller stops waiting for a check, because the check was cancelled or its
time limit passed, VerificationTask.StartRun and SplitAndVerifyWorker.DoWork
returned the checker to the pool while Split.BeginCheck, or the prover task it
started, was still sending commands to the solver. The next check to take the
checker then shared the solver process with the abandoned one. Their commands
interleave, and since SMTLibProcess.Send is not synchronized they also tear, so
the solver answers "invalid command, '(' expected", or reports ControlFlow as
already declared, or names symbols the check never sent, such as
$generated@@26. The two checks also share the prover's state in memory. Either
way the next check reports a wrong result, or waits forever for an answer.

GoBackToIdle now takes the task of the check its caller began, and closes the
checker instead of pooling it while that check or its prover task still runs. It
also closes the checker when the ping that tests the prover fails for any
reason; before, only a timeout did, and any other exception escaped GoBackToIdle
without freeing the checker's slot in the pool. CheckerDied ignores a disposed
pool, as AddChecker does: releasing the disposed semaphore threw before
GoBackToIdle could close the checker, so its solver process stayed alive.

DoWork gives its checker back in one place, a finally, and before the splits
that a failed split is split into are verified, so that they can take it.
Before, ProcessResultAndReleaseChecker gave it back and DoWork's catch gave it
back again when one of those splits failed, so the pool could hold the same
checker twice and hand it to two checks at once.

A check whose BeginCheck outlasted its time limit crashed Boogie on the command
line, where DoWork let the TimeoutException escape (#1064), and ended a language
server's statuses with that exception instead of a result. Both paths now wait
through Split.WaitForBeginCheck, which reports such a check as timed out, so
that keep-going splitting can split it further, and close its checker, since the
abandoned BeginCheck may still be using it.

#932 opened the window by letting StartRun and DoWork stop waiting for
BeginCheck; in a language server, a cancelled check could abandon its prover
task since #910, which made StartRun wait for it through WaitAsync. Dafny takes
that path for every check since it set a default time limit
(dafny-lang/dafny#6028), and the race is behind its flaky tests
dafny-lang/dafny#2316, which cancels, and dafny-lang/dafny#5798, which times
out. Keep-going splits run with a time limit of a second by default, which is
how #1064 reached it.

Eight tests give a check a solver that counts the Sends overlapping on it, and
block one Send to hold the check open. They cover cancellation and a time limit
on both paths, the prover-task phase, a broken pipe, a failed keep-going split
and a disposed engine. All eight fail on master.
```

## Review notes for a successor

What the review changed, so it is not redone:

- Rebased onto `751228a2` (the incoming commit touches only Civl `.bpl` samples; no interaction).
- Squashed 6 commits to 1. Two were fixups, one carrying a mangled `…limit as timed out` fragment
  as its body. No proper subset of the fix is independently testable, so a split would be artificial.
- `DoWork` now releases the checker in a single `finally` instead of via a mutable `released` flag
  and a `Func<Task>` threaded through two methods, which also removed dead code carried over from
  before (a `with` expression whose result was never read, and which was a no-op anyway because
  `CounterExamples` is the same live list).
- Dropped `CheckerDiscardedAfterItsEngineIsDisposedIsClosed`: identical path through
  `GoBackToIdle` to the timed-out disposal test, a strict subset of its assertions, and it passes
  on master.
- `FailedKeepGoingSplitReleasesTheCheckerOfItsParentOnce` could pass vacuously — with
  `timeOutFirstCheck` broken, no split happens, the pool holds one checker and `AreNotSame`
  succeeds for the wrong reason. Added an `InconclusiveCount` guard, ordered after the primary
  assertion so master still fails on the meaningful one.

Description claims corrected during review, all verified against a rebuilt master, not against the
previous commit:

- "On master seven fail" → eight of nine did.
- "The two disposal tests pass on master" → only one did.
- Four of six quoted error strings were unsourceable. Present in the cited issues: `invalid
  command, '(' expected` and `$generated@@26` (dafny#2316 and dafny#5798, verbatim), and
  `ControlFlow` already declared (#1064). Absent everywhere: `unknown constant $generated@@37` —
  the real line is `Prover error: ; $generated@@26 line: 101 position: 207`, with no "unknown
  constant" — plus `NullReferenceException` and `Collection was modified`.
- A Scope sentence quoted "15 overlapping sends" for a build that was never compiled (the
  time-limit part alone); the number was measured on full-fix-minus-the-in-flight-check.
- A lit sentence implied the same pair of tests fails in both `batch_mode` configs. It does not:
  49 files carry `UNSUPPORTED: batch_mode`, so `test21/InterestingExamples4.bpl` is skipped under
  `True`, and `prover/z3-hard-timeout.bpl` flips on machine load rather than on the config. Note
  lit reports 671 discovered in both configs because an unsupported test still counts as
  discovered, so failure sets never compare across configs.

Found while diagnosing a lit failure, deliberately **not** fixed here (wrong scope; #1170 does not
touch `ExecutionEngine.cs`): `PipelineOutcome.Cancelled` has no case in
`ExecutionEngine.ProcessProgram`'s switch (`ExecutionEngine.cs:202`), so a cancelled command-line
run hits `Debug.Assert(false, "Unreachable code")` and FailFasts in Debug; and
`VerifyEachImplementation` catches `TaskCanceledException` but not plain `OperationCanceledException`,
so cancellation leaves by two different routes. **#1172 owns both and has them covered**: its head
is `eadc1ad8` (2 files, +86/-4, updated 2026-11-03T17:12Z), which fixes those two plus
`PreProcessProgramVerification`'s token and `ProcessFiles`' `ThrowIfCancellationRequested` loops.
An earlier revision of this file said that PR was stranded at `84c377d2` with its session ended;
both were wrong, and the claim came from a peer session's stale `gh` read rather than from a check.

Also unfiled: `prover/z3-hard-timeout.bpl` fails on master with modern z3 (it CHECKs for a
resource count of `-1`, which only appears when z3 is killed by `-T:1`), on four independent
measurements. It is load-dependent in the backwards direction — it fails on an idle machine and
passes on a busy one — and it is not gated by `batch_mode`, since its own RUN line forces batch
mode.
