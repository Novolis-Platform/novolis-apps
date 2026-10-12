using PresenceLedger.App;

namespace PresenceLedger.App.Tests;

public sealed class LedgerStorageStatusTests
{
    [Test]
    public async Task Manual_import_updates_the_visible_copy_count()
    {
        var status = new LedgerStorageStatus("Internal", 0, null);

        status.RecordImport(2, errorMessage: null);

        await Assert.That(status.ImportedFileCount).IsEqualTo(2);
        await Assert.That(status.ImportError).IsNull();
    }

    [Test]
    public async Task Import_error_is_retained_without_erasing_the_copy_count()
    {
        var status = new LedgerStorageStatus("Internal", 1, null);

        status.RecordImport(0, "folder unavailable");

        await Assert.That(status.ImportedFileCount).IsEqualTo(1);
        await Assert.That(status.ImportError).IsEqualTo("folder unavailable");
    }
}
