# Moq / NSubstitute Bidirectional Mapping Matrix

Comprehensive mapping of mock framework patterns covered by the example test suites.
Each row is exercised by at least one test in both directions.

## Legend

| Safety | Meaning |
|--------|---------|
| **safe** | Direct structural mapping; automated rewrite expected to be correct |
| **review** | Requires restructuring or semantic review; automated output needs human check |
| **manual** | No equivalent or fundamentally different paradigm; must be rewritten by hand |

## Pattern Matrix

| # | Pattern | Moq | NSubstitute | Safety | Notes |
|---|---------|-----|-------------|--------|-------|
| 1 | Mock creation | `new Mock<T>()` | `Substitute.For<T>()` | safe | NSub returns T directly; Moq wraps in Mock\<T\> |
| 2 | Access underlying object | `mock.Object` | _(substitute is T)_ | safe | NSub has no indirection |
| 3 | Setup returns (value) | `mock.Setup(x => x.M()).Returns(v)` | `sub.M().Returns(v)` | safe | |
| 4 | Setup returns (computed) | `mock.Setup(x => x.M(any)).Returns((T a) => expr)` | `sub.M(any).Returns(ci => expr)` | review | Moq passes args directly; NSub uses CallInfo |
| 5 | Setup returns (async) | `mock.Setup(x => x.M()).ReturnsAsync(v)` | `sub.M().Returns(v)` | safe | NSub auto-wraps in Task\<T\> |
| 6 | Setup returns null | `mock.Setup(x => x.M(any)).Returns((T?)null)` | `sub.M(any).Returns((T?)null)` | safe | |
| 7 | Sequential returns | `mock.SetupSequence(x => x.M()).Returns(a).Returns(b)` | `sub.M().Returns(a, b)` | safe | NSub uses params array |
| 8 | Setup throws | `mock.Setup(x => x.M()).Throws(ex)` | `sub.M().Throws(ex)` | safe | NSub needs `using NSubstitute.ExceptionExtensions` |
| 9 | Setup throws async | `mock.Setup(x => x.M()).ThrowsAsync(ex)` | `sub.M().ThrowsAsync(ex)` | safe | |
| 10 | Callback (void method) | `mock.Setup(x => x.M(any)).Callback<T>(action)` | `sub.When(x => x.M(any)).Do(ci => action)` | review | Different structural pattern |
| 11 | Callback (return method) | `mock.Setup(x => x.M()).Returns(v).Callback(action)` | `sub.M().Returns(ci => { action; return v; })` | review | NSub combines in Returns lambda |
| 12 | Arg.Do (inline capture) | `mock.Setup(x => x.M(any)).Callback<T>(a => list.Add(a))` | `sub.M(Arg.Do<T>(a => list.Add(a)))` | review | Structural difference; Moq->NSub simplifies, reverse is harder |
| 13 | Property get | `mock.Setup(x => x.Prop).Returns(v)` | `sub.Prop.Returns(v)` | safe | |
| 14 | Property set verify | `mock.VerifySet(x => x.Prop = v)` | `sub.Received().Prop = v` | safe | |
| 15 | Verify once | `mock.Verify(x => x.M(), Times.Once)` | `sub.Received(1).M()` | safe | |
| 16 | Verify never | `mock.Verify(x => x.M(), Times.Never)` | `sub.DidNotReceive().M()` | safe | |
| 17 | Verify exactly N | `mock.Verify(x => x.M(), Times.Exactly(n))` | `sub.Received(n).M()` | safe | |
| 18 | Verify at least once | `mock.Verify(x => x.M(), Times.AtLeastOnce)` | `sub.Received().M()` | safe | Received() with no count = at-least-once |
| 19 | Arg any | `It.IsAny<T>()` | `Arg.Any<T>()` | safe | |
| 20 | Arg predicate | `It.Is<T>(pred)` | `Arg.Is<T>(pred)` | safe | |
| 21 | Arg range | `It.IsInRange(lo, hi, Range.Inclusive)` | `Arg.Is<T>(x => x >= lo && x <= hi)` | safe | NSub uses predicate; no built-in range matcher |
| 22 | Out parameter | `It.Ref<T>.IsAny` + delegate Returns | `Arg.Any<T>()` + `ci[index] = val` in Returns | review | Fundamentally different setup mechanism |
| 23 | Event raising | `mock.Raise(x => x.E += null, args)` | `sub.E += Raise.EventWith(args)` | safe | |
| 24 | Generic interface | `new Mock<ICache<T>>()` | `Substitute.For<ICache<T>>()` | safe | Same as basic creation |
| 25 | Overloaded methods | Standard Setup/Returns per overload | Standard Returns per overload | safe | |
| 26 | MockBehavior.Strict | `new Mock<T>(MockBehavior.Strict)` | _(no equivalent)_ | manual | NSub always returns defaults for unconfigured calls |
| 27 | VerifyAll | `mock.VerifyAll()` | _(verify each call with Received)_ | manual | Must enumerate each expectation |
| 28 | VerifyNoOtherCalls | `mock.VerifyNoOtherCalls()` | _(no equivalent)_ | manual | Cannot detect unexpected calls in NSub |
| 29 | ReceivedWithAnyArgs | `mock.Verify(x => x.M(It.IsAny<...>()))` | `sub.ReceivedWithAnyArgs().M()` | review | Moq must enumerate each param with IsAny |
| 30 | Default behavior | Returns defaults (Loose) | Returns defaults | safe | Both return 0/false/null for unconfigured calls |

## Coverage Summary

| Safety | Count | Percentage |
|--------|-------|------------|
| safe | 20 | 67% |
| review | 7 | 23% |
| manual | 3 | 10% |

## Test Coverage

Each direction contains **25 tests** covering the patterns above:

- `MoqToNSubstitute/SampleTests.Before/` -- 25 Moq tests
- `MoqToNSubstitute/SampleTests.After/` -- 25 equivalent NSubstitute tests
- `NSubstituteToMoq/SampleTests.Before/` -- 25 NSubstitute tests
- `NSubstituteToMoq/SampleTests.After/` -- 25 equivalent Moq tests
