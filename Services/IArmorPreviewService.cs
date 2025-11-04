using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RequiemGlamPatcher.Models;
using RequiemGlamPatcher.ViewModels;

namespace RequiemGlamPatcher.Services;

public interface IArmorPreviewService
{
    Task<ArmorPreviewScene> BuildPreviewAsync(
        IEnumerable<ArmorRecordViewModel> armorPieces,
        GenderedModelVariant preferredGender,
        CancellationToken cancellationToken = default);
}
