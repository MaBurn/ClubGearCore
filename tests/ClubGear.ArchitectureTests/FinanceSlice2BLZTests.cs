using ClubGear.Plugin.Finance;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// Slice 2 verification: BundesbankBlzService — IBAN-to-BIC/BankName lookup via embedded CSV.
/// </summary>
public sealed class FinanceSlice2BLZTests
{
    [Fact]
    public void LookupByIban_ValidDeIban_ReturnsNonNullBicAndBankName()
    {
        // BLZ 37040044 = Commerzbank Köln (COBADEFFXXX)
        var (bic, bankName) = BundesbankBlzService.LookupByIban("DE89370400440532013000");

        Assert.NotNull(bic);
        Assert.NotNull(bankName);
        Assert.NotEmpty(bic);
        Assert.NotEmpty(bankName);
    }

    [Fact]
    public void LookupByIban_NonDeIban_ReturnsNullTuple()
    {
        var (bic, bankName) = BundesbankBlzService.LookupByIban("AT611904300234573201");

        Assert.Null(bic);
        Assert.Null(bankName);
    }

    [Fact]
    public void LookupByIban_UnknownBlz_ReturnsNullTuple()
    {
        // BLZ 00000000 does not exist in the Bundesbank file
        var (bic, bankName) = BundesbankBlzService.LookupByIban("DE00000000000000000000");

        Assert.Null(bic);
        Assert.Null(bankName);
    }

    [Fact]
    public void LookupByIban_SecondCallReturnsSameResult_CacheExercised()
    {
        const string iban = "DE89370400440532013000";

        var first = BundesbankBlzService.LookupByIban(iban);
        var second = BundesbankBlzService.LookupByIban(iban);

        // Both calls must agree — verifies the cache path is exercised and stable
        Assert.Equal(first.Bic, second.Bic);
        Assert.Equal(first.BankName, second.BankName);
        Assert.NotNull(second.Bic);
        Assert.NotNull(second.BankName);
    }
}
