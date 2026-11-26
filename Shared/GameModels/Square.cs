namespace Checkers.Shared.GameModels
{
	public readonly record struct Square(int Row, int Col)
	{
		public override string ToString() => $"({Row}, {Col})";
	}
}
