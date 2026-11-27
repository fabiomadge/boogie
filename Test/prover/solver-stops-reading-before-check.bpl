// A solver that stops reading before the check, but stays alive, gives the check a solver exception without being
// waited for, however many writes fail first.
// RUN: %boogie /proverOpt:PROVER_PATH=stops-reading-at-first-ping.sh "%s" | %OutputCheck "%s"
// CHECK-L: Verification encountered solver exception (P)
// CHECK-L: Boogie program verifier finished with 0 verified, 0 errors, 1 solver exceptions

procedure P(x: int) {
  assert x == x;
}
