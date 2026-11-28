// A solver that reads its input but never answers used to hang the run: /timeLimit is sent to the solver, which
// asks a solver that is answering to give up, and this one is not. Boogie's own deadline ends the check instead.
// RUN: %boogie /timeLimit:2 /proverOpt:PROVER_PATH=silent-solver.sh "%s" > "%t"
// RUN: %OutputCheck --file-to-check "%t" "%s"
// CHECK-L: Error: this assertion could not be proved
// CHECK-L: Boogie program verifier finished with 0 verified, 1 error

// Batch mode closes the solver's input, so it reaches the end of its output rather than waiting for an answer.
// UNSUPPORTED: batch_mode

procedure P(x: int) {
  assert x == x;
}
