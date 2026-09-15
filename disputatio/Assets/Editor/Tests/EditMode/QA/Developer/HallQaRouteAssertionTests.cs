#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Godlotto.QA.SceneAdapters;
using NUnit.Framework;

/// <summary>
/// AC07: Hall assert-route must require Kitchen arrival, transition end, and open input gate.
/// Controller presence in Hall_playerble is not a pass.
/// </summary>
public class HallQaRouteAssertionTests
{
    [Test]
    public void Evaluate_ControllerFoundInHall_IsNotPass()
    {
        HallQaRouteAssertionResult result = HallQaRouteAssertion.Evaluate(
            new Dictionary<string, string>
            {
                ["controllerFound"] = "True",
                ["activeScene"] = "Hall_playerble",
                ["transitionPending"] = "False",
                ["inputGateBlocked"] = "False"
            });

        Assert.IsFalse(result.Passed);
        CollectionAssert.Contains(result.ReasonCodes, "controller-only");
        CollectionAssert.Contains(result.ReasonCodes, "destination-mismatch");
    }

    [Test]
    public void Evaluate_KitchenWithTransitionClearedAndGateOpen_Passes()
    {
        HallQaRouteAssertionResult result = HallQaRouteAssertion.Evaluate(
            new Dictionary<string, string>
            {
                ["controllerFound"] = "False",
                ["activeScene"] = "Kitchen",
                ["transitionPending"] = "False",
                ["inputGateBlocked"] = "False"
            });

        Assert.IsTrue(result.Passed);
        Assert.IsEmpty(result.ReasonCodes);
    }

    [Test]
    public void Evaluate_KitchenWhileTransitionPending_Fails()
    {
        HallQaRouteAssertionResult result = HallQaRouteAssertion.Evaluate(
            new Dictionary<string, string>
            {
                ["controllerFound"] = "False",
                ["activeScene"] = "Kitchen",
                ["transitionPending"] = "True",
                ["inputGateBlocked"] = "False"
            });

        Assert.IsFalse(result.Passed);
        CollectionAssert.Contains(result.ReasonCodes, "transition-pending");
    }

    [Test]
    public void Evaluate_KitchenWhileInputGateBlocked_Fails()
    {
        HallQaRouteAssertionResult result = HallQaRouteAssertion.Evaluate(
            new Dictionary<string, string>
            {
                ["controllerFound"] = "False",
                ["activeScene"] = "Kitchen",
                ["transitionPending"] = "False",
                ["inputGateBlocked"] = "True"
            });

        Assert.IsFalse(result.Passed);
        CollectionAssert.Contains(result.ReasonCodes, "input-gate-blocked");
    }
}
#endif
