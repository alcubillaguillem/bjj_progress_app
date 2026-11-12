using BjjJournal.Application.Services;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace BjjJournal.Application.Tests;

public class AuthServiceTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero));
    private readonly FakeKeyValueStore _store = new();
    private readonly FakeAuthApi _api;
    private readonly AuthService _auth;

    public AuthServiceTests()
    {
        _api = new FakeAuthApi(_time);
        _auth = new AuthService(_api, _store, _time);
    }

    [Fact]
    public async Task SignInAsync_RightPassword_SavesSessionAndClearsGuest()
    {
        await _auth.SetGuestAsync(true);
        (await _auth.SignInAsync("ana@test.com", "secreto1")).IsSuccess.ShouldBeTrue();
        _auth.IsSignedIn.ShouldBeTrue();
        _store.Values.ShouldContainKey(AuthService.SessionKey);
        (await _auth.IsGuestAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task SignInAsync_WrongPassword_Fails()
    {
        var result = await _auth.SignInAsync("ana@test.com", "mal");
        result.IsSuccess.ShouldBeFalse();
        _auth.IsSignedIn.ShouldBeFalse();
    }

    [Fact]
    public async Task SignInAsync_EmptyEmail_FailsWithoutCallingApi() =>
        (await _auth.SignInAsync(" ", "x")).Error.ShouldBe("Escribe tu correo y tu contraseña.");

    [Fact]
    public async Task SignUpAsync_ShortPassword_Fails() =>
        (await _auth.SignUpAsync("ana@test.com", "123", "https://app/")).IsSuccess.ShouldBeFalse();

    [Fact]
    public async Task SignUpAsync_EmailConfirmationRequired_ReturnsFalseAndNoSession()
    {
        _api.RequireEmailConfirmation = true;
        var result = await _auth.SignUpAsync("ana@test.com", "secreto1", "https://app/");
        result.Value.ShouldBeFalse();
        _auth.IsSignedIn.ShouldBeFalse();
    }

    [Fact]
    public async Task InitializeAsync_GoogleRedirectFragment_CreatesSession()
    {
        var token = Jwt.Make(_api.UserId, "ana@gmail.com", _time.GetUtcNow().AddHours(1));
        var uri = $"https://app/#access_token={token}&expires_in=3600&refresh_token=r1&token_type=bearer";

        (await _auth.InitializeAsync(uri)).ShouldBeNull();
        _auth.Session!.UserId.ShouldBe(_api.UserId);
        _auth.Session.Email.ShouldBe("ana@gmail.com");
        _auth.Session.RefreshToken.ShouldBe("r1");
    }

    [Fact]
    public async Task InitializeAsync_ErrorFragment_ReturnsMessage()
    {
        var error = await _auth.InitializeAsync("https://app/#error=access_denied&error_description=Usuario+cancel%C3%B3");
        error.ShouldBe("Usuario canceló");
        _auth.IsSignedIn.ShouldBeFalse();
    }

    [Fact]
    public async Task InitializeAsync_StoredSession_IsRestored()
    {
        await _auth.SignInAsync("ana@test.com", "secreto1");
        var other = new AuthService(_api, _store, _time);
        await other.InitializeAsync("https://app/");
        other.Session!.Email.ShouldBe("ana@test.com");
    }

    [Fact]
    public async Task GetValidSessionAsync_TokenAboutToExpire_Refreshes()
    {
        await _auth.SignInAsync("ana@test.com", "secreto1");
        _time.Advance(TimeSpan.FromMinutes(59));
        (await _auth.GetValidSessionAsync()).ShouldNotBeNull();
        _api.Refreshes.ShouldBe(1);
    }

    [Fact]
    public async Task GetValidSessionAsync_RefreshRejected_SignsOut()
    {
        await _auth.SignInAsync("ana@test.com", "secreto1");
        _api.FailRefresh = true;
        _time.Advance(TimeSpan.FromHours(2));
        (await _auth.GetValidSessionAsync()).ShouldBeNull();
        _auth.IsSignedIn.ShouldBeFalse();
        _store.Values.ShouldNotContainKey(AuthService.SessionKey);
    }

    [Fact]
    public async Task SignOutAsync_ClearsSession()
    {
        await _auth.SignInAsync("ana@test.com", "secreto1");
        await _auth.SignOutAsync();
        _auth.IsSignedIn.ShouldBeFalse();
        _store.Values.ShouldNotContainKey(AuthService.SessionKey);
    }
}
