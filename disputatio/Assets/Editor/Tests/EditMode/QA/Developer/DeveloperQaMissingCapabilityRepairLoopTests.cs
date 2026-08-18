#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Developer;
using Godlotto.QA.SceneAdapters;
using Godlotto.QA.Scenes;
using NUnit.Framework;

/// <summary>
/// Task 12 (Unity layer, best-effort): empty registry → MissingCapability →
/// RegisterCapabilities (patch simulation) → describe/list includes place-bookmark.
/// Does not require PlayMode / StudyRoom scene load.
/// </summary>
public class DeveloperQaMissingCapabilityRepairLoopTests
{
    [Test]
    public async Task EmptyRegistry_InvokePlaceBookmark_ThenRegister_ListAndDescribeIncludeId()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        var service = new DeveloperQaService(registry);
        string placeBookmarkId = StudyRoomQaAdapter.PlaceBookmarkCapabilityId;

        Assert.AreEqual(0, service.ListCapabilities().Count);

        DeveloperQaResult missing = await service.ExecuteAsync(
            DeveloperQaCommand.Create(
                "e2e-missing",
                "interaction",
                "invoke",
                placeBookmarkId),
            CancellationToken.None);

        Assert.AreEqual(DeveloperQaResultCode.MissingCapability, missing.Code);
        Assert.AreEqual(placeBookmarkId, missing.MissingCapabilityId);
        Assert.IsFalse(string.IsNullOrEmpty(missing.CheckpointId));

        // Simulate external autorun QA capability patch: register StudyRoom caps.
        StudyRoomQaAdapter.RegisterCapabilities(registry);

        Assert.IsTrue(
            service.ListCapabilities().Any(c => c.Id == placeBookmarkId),
            "RegisterCapabilities must expose place-bookmark after patch simulation.");

        DeveloperQaResult list = await service.ExecuteAsync(
            DeveloperQaCommand.Create("e2e-list", "capability", "list"),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.Ok, list.Code);
        Assert.IsTrue(
            list.Data.ContainsKey("current_capabilities") &&
            list.Data["current_capabilities"].Contains(placeBookmarkId),
            "capability.list must include place-bookmark after register.");

        DeveloperQaResult describe = await service.ExecuteAsync(
            DeveloperQaCommand.Create(
                "e2e-describe",
                "capability",
                "describe",
                placeBookmarkId),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.Ok, describe.Code);
        Assert.AreEqual(placeBookmarkId, describe.Data["id"]);
        Assert.AreEqual(SceneNames.StudyRoom, describe.Data["scene_id"]);
    }
}
#endif
