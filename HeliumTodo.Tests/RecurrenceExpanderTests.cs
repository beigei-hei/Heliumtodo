using HeliumTodo.Core.Services;
using Xunit;

namespace HeliumTodo.Tests;

/// <summary>重复规则解析的测试。</summary>
public class RecurrenceExpanderTests
{
    [Fact]
    public void 空规则表示不重复()
    {
        Assert.Empty(RecurrenceExpander.Parse(""));
        Assert.Empty(RecurrenceExpander.Parse(null));
        Assert.Empty(RecurrenceExpander.Parse("   "));
    }

    [Fact]
    public void daily解析成一周七天()
    {
        var days = RecurrenceExpander.Parse("daily");
        Assert.Equal(7, days.Count);
    }

    [Fact]
    public void weekly解析出指定的星期几()
    {
        var days = RecurrenceExpander.Parse("weekly:1,3,5");

        Assert.Equal(3, days.Count);
        Assert.Contains(DayOfWeek.Monday, days);
        Assert.Contains(DayOfWeek.Wednesday, days);
        Assert.Contains(DayOfWeek.Friday, days);
        Assert.DoesNotContain(DayOfWeek.Sunday, days);
    }

    [Fact]
    public void weekly的大小写和空格都能容忍()
    {
        var days = RecurrenceExpander.Parse("  WEEKLY : 1 , 3 , 5  ");
        Assert.Equal(3, days.Count);
        Assert.Contains(DayOfWeek.Monday, days);
        Assert.Contains(DayOfWeek.Friday, days);
    }

    [Fact]
    public void 重复的星期会自动去重()
    {
        var days = RecurrenceExpander.Parse("weekly:1,1,1");
        Assert.Single(days);
    }

    [Fact]
    public void 认不出来的规则当成不重复_不能抛异常()
    {
        // 脏数据不能让程序崩掉
        Assert.Empty(RecurrenceExpander.Parse("每两周一次"));
        Assert.Empty(RecurrenceExpander.Parse("weekly:99"));
        Assert.Empty(RecurrenceExpander.Parse("weekly:abc"));
    }

    [Fact]
    public void 描述文字正确()
    {
        Assert.Equal("单次", RecurrenceExpander.Describe(""));
        Assert.Equal("每天", RecurrenceExpander.Describe("daily"));
        Assert.Equal("每周一、三、五", RecurrenceExpander.Describe("weekly:1,3,5"));
        Assert.Equal("每周日", RecurrenceExpander.Describe("weekly:0"));
    }
}
