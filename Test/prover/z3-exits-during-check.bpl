// A solver that runs out of its own hard time limit reports a timeout, the same way interactively as in batch
// mode. z3 prints `timeout` and exits 0, so the answer is there to be read, and the check that used it does not
// look like one whose solver died.
// RUN: %boogie /proverOpt:C:"-T:1" "%s" | %OutputCheck "%s"
// CHECK-L: Error: this assertion could not be proved
// CHECK-L: Error: this assertion could not be proved
// CHECK-L: Boogie program verifier finished with 0 verified, 2 errors

// z3 decides neither assertion before its hard time limit of a second makes it exit.
procedure Cubes1(x: int, y: int, z: int) {
  assume x > 1 && y > 1 && z > 1;
  assert x * x * x + y * y * y != z * z * z;
}

// Checked only if the failed check above gives up its place in the checker pool.
procedure Cubes2(x: int, y: int, z: int) {
  assume x > 1 && y > 1 && z > 1;
  assert x * x * x + y * y * y != z * z * z;
}
