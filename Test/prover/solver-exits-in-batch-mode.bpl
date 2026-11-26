// A solver that exits without answering is reported as dead instead of being waited for forever, and in batch mode
// the same way as interactively.
// RUN: %boogie /proverOpt:BATCH_MODE=true /proverOpt:PROVER_PATH=silent-solver.sh "%s" | %OutputCheck "%s"
// CHECK-L: Verification encountered solver exception (P)
// CHECK-L: Boogie program verifier finished with 0 verified, 0 errors, 1 solver exceptions

procedure P(x: int) {
  assert x > 0;
}
