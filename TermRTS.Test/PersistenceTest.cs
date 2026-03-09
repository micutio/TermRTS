namespace TermRTS.Test;

public class PersistenceTest
{
    private static Scheduler NewScheduler()
    {
        return new Scheduler(new Core { Renderer = new NullRenderer() });
    }

    [Fact]
    public void GetSimStateFromJson_null_returns_false_and_error_response()
    {
        var scheduler = NewScheduler();
        var persistence = new Persistence();

        var success = persistence.GetSimStateFromJson(ref scheduler, null, out var response);

        Assert.False(success);
        Assert.Contains("empty json", response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetSimStateFromJson_empty_string_returns_false_and_error_response()
    {
        var scheduler = NewScheduler();
        var persistence = new Persistence();

        var success = persistence.GetSimStateFromJson(ref scheduler, "", out var response);

        Assert.False(success);
        Assert.Contains("empty json", response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetSimStateFromJson_whitespace_only_returns_false_and_error_response()
    {
        var scheduler = NewScheduler();
        var persistence = new Persistence();

        var success = persistence.GetSimStateFromJson(ref scheduler, "   \t\n  ", out var response);

        Assert.False(success);
        Assert.Contains("empty json", response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetSimStateFromJson_invalid_json_returns_false_and_error_response()
    {
        var scheduler = NewScheduler();
        var persistence = new Persistence();
        const string invalidJson = "{ not valid json }";

        var success = persistence.GetSimStateFromJson(ref scheduler, invalidJson, out var response);

        Assert.False(success);
        Assert.Contains("invalid json", response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetSimStateFromJson_json_null_literal_returns_false_and_invalid_state_response()
    {
        var scheduler = NewScheduler();
        var persistence = new Persistence();
        const string jsonNull = "null";

        var success = persistence.GetSimStateFromJson(ref scheduler, jsonNull, out var response);

        Assert.False(success);
        Assert.Contains("invalid", response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetSimStateFromJson_valid_json_from_put_returns_true_and_success_response()
    {
        var scheduler = NewScheduler();
        var persistence = new Persistence();
        var putSuccess = persistence.PutSimStateToJson(ref scheduler, out var jsonStr, out _);
        Assert.True(putSuccess);
        Assert.NotNull(jsonStr);

        var getSuccess = persistence.GetSimStateFromJson(ref scheduler, jsonStr, out var response);

        Assert.True(getSuccess);
        Assert.Contains("deserialized", response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PutSimStateToJson_success_returns_true_and_sets_response()
    {
        var scheduler = NewScheduler();
        var persistence = new Persistence();

        var success = persistence.PutSimStateToJson(ref scheduler, out var jsonStr, out var response);

        Assert.True(success);
        Assert.NotNull(jsonStr);
        Assert.Contains("serialized", response, StringComparison.OrdinalIgnoreCase);
    }
}
