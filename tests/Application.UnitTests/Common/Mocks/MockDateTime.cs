namespace Application.UnitTests.Common.Mocks;

public class MockDateTime : IDateTime
{
    private DateTime _now;

    public MockDateTime() : this(DateTime.UtcNow)
    {
    }

    public MockDateTime(DateTime fixedTime)
    {
        _now = fixedTime;
    }

    public DateTime Now => _now;

    public void SetNow(DateTime dateTime)
    {
        _now = dateTime;
    }

    public void Advance(TimeSpan timeSpan)
    {
        _now = _now.Add(timeSpan);
    }

    public static MockDateTime Create(DateTime? fixedTime = null)
    {
        return new MockDateTime(fixedTime ?? DateTime.UtcNow);
    }

    public static Mock<IDateTime> CreateMock(DateTime? fixedTime = null)
    {
        var mock = new Mock<IDateTime>();
        mock.Setup(x => x.Now).Returns(fixedTime ?? DateTime.UtcNow);
        return mock;
    }
}
