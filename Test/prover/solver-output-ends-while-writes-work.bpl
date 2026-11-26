// A solver whose output ends while Boogie can still write to it fails the check that used it, and the next one,
// instead of stopping the run. Which of the two a dead solver is noticed by is a race, so both have to report it
// the same way.
// RUN: %boogie /proverOpt:PROVER_PATH=mute-solver.sh "%s" | %OutputCheck "%s"
// CHECK-L: Verification encountered solver exception (P)
// CHECK-L: Verification encountered solver exception (Q)
// CHECK-L: Boogie program verifier finished with 0 verified, 0 errors, 2 solver exceptions

procedure P(x: int) {
  assert x > 0;
}

procedure Q(x: int) {
  assert x > 0;
}
