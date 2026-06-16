using NUnit.Framework;

public class TimeFormatTests
{
    [Test] public void Hms_Hours()       => Assert.AreEqual("8h 33m 9s", TimeFormat.Hms(8 * 3600 + 33 * 60 + 9));
    [Test] public void Hms_MinutesOnly()  => Assert.AreEqual("33m 9s",    TimeFormat.Hms(33 * 60 + 9));
    [Test] public void Hms_SecondsOnly()  => Assert.AreEqual("45s",       TimeFormat.Hms(45));
    [Test] public void Hms_Zero()         => Assert.AreEqual("0s",        TimeFormat.Hms(0));
    [Test] public void Hms_DropsZeroMid()  => Assert.AreEqual("2h 0m 5s",  TimeFormat.Hms(2 * 3600 + 5)); // keep middle unit once a larger one shows
    [Test] public void Hms_ExactHour()     => Assert.AreEqual("1h 0m 0s",  TimeFormat.Hms(3600));
    [Test] public void Hms_NegativeClamps() => Assert.AreEqual("0s",       TimeFormat.Hms(-12));
}
