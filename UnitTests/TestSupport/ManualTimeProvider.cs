namespace UnitTests.TestSupport
{
	/// <summary>
	/// A clock that only moves when a test moves it, for turn time limits.
	/// </summary>
	public sealed class ManualTimeProvider : TimeProvider
	{
		private DateTimeOffset _now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

		public override DateTimeOffset GetUtcNow() => _now;

		public void Advance(TimeSpan by) => _now += by;
	}
}
