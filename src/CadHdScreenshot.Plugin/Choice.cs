namespace CadHdScreenshot.Plugin
{
	internal sealed class Choice<T>
	{
		public string Label { get; }

		public T Value { get; }

		public Choice(string label, T value)
		{
			Label = label;
			Value = value;
		}

		public override string ToString()
		{
			return Label;
		}
	}
}
