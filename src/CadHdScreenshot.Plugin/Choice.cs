namespace CadHdScreenshot.Plugin
{
    internal sealed class Choice<T>
    {
        public Choice(string label, T value)
        {
            Label = label;
            Value = value;
        }

        public string Label { get; }

        public T Value { get; }

        public override string ToString()
        {
            return Label;
        }
    }
}
