// A solver exception leaves a non-zero exit code, so the output goes through a pipe rather than a file.
// RUN: %parallel-boogie /proverOpt:PROVER_PATH=/usr/bin/false "%s" | %OutputCheck "%s"
// CHECK: exitcode.bpl\(6,11\): Verification encountered solver exception \(P\)
// CHECK: Boogie program verifier finished with 0 verified, 0 errors, 1 solver exceptions

procedure P(x: int, y: int) {
  assert x*y == y*x;
}
