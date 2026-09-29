#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Godlotto.QA.SceneAdapters;
using NUnit.Framework;

/// <summary>
/// BasementHallway assert-route: destination arrival, transition end, open input gate.
/// Controller presence in the hallway alone is not a pass.
/// </summary>
public class BasementHallwayQaRouteAssertionTests
{
    [Test]
    public void Evaluate_ControllerFoundInHallway_IsNotPass()
    {
        BasementHallwayQaRouteAssertionResult result = BasementHallwayQaRouteAssertion.Evaluate(
            new Dictionary<string, string>
            {
                ["controllerFound"] = "True",
                ["activeScene"] = "BasementHallway",
                ["transitionPending"] = "False",
                ["inputGateBlocked"] = "False"
            },
            expectedScene: "BasementBrickRoom");

        Assert.IsFalse(result.Passed);
        CollectionAssert.Contains(result.ReasonCodes, BasementHallwayQaRouteAssertion.ReasonControllerOnly);
        CollectionAssert.Contains(result.ReasonCodes, BasementHallwayQaRouteAssertion.ReasonDestinationMismatch);
    }

    [Test]
    public void Evaluate_ExpectedDestinationWithTransitionClearedAndGateOpen_Passes(
        [Values(
            "BasementBrickRoom",
            "BasementExtractionRoom",
            "BasementObservationRoom",
            "BasementResearchRoom",
            "Basement")] string destination)
    {
        BasementHallwayQaRouteAssertionResult result = BasementHallwayQaRouteAssertion.Evaluate(
            new Dictionary<string, string>
            {
                ["controllerFound"] = "False",
                ["activeScene"] = destination,
                ["transitionPending"] = "False",
                ["inputGateBlocked"] = "False"
            },
            expectedScene: destination);

        Assert.IsTrue(result.Passed);
        Assert.IsEmpty(result.ReasonCodes);
    }

    [Test]
    public void Evaluate_WrongDestination_Fails()
    {
        BasementHallwayQaRouteAssertionResult result = BasementHallwayQaRouteAssertion.Evaluate(
            new Dictionary<string, string>
            {
                ["controllerFound"] = "False",
                ["activeScene"] = "BasementExtractionRoom",
                ["transitionPending"] = "False",
                ["inputGateBlocked"] = "False"
            },
            expectedScene: "BasementBrickRoom");

        Assert.IsFalse(result.Passed);
        CollectionAssert.Contains(result.ReasonCodes, BasementHallwayQaRouteAssertion.ReasonDestinationMismatch);
    }

    [Test]
    public void Evaluate_DestinationWhileTransitionPending_Fails()
    {
        BasementHallwayQaRouteAssertionResult result = BasementHallwayQaRouteAssertion.Evaluate(
            new Dictionary<string, string>
            {
                ["controllerFound"] = "False",
                ["activeScene"] = "BasementBrickRoom",
                ["transitionPending"] = "True",
                ["inputGateBlocked"] = "False"
            },
            expectedScene: "BasementBrickRoom");

        Assert.IsFalse(result.Passed);
        CollectionAssert.Contains(result.ReasonCodes, BasementHallwayQaRouteAssertion.ReasonTransitionPending);
    }

    [Test]
    public void Evaluate_DestinationWhileInputGateBlocked_Fails()
    {
        BasementHallwayQaRouteAssertionResult result = BasementHallwayQaRouteAssertion.Evaluate(
            new Dictionary<string, string>
            {
                ["controllerFound"] = "False",
                ["activeScene"] = "BasementBrickRoom",
                ["transitionPending"] = "False",
                ["inputGateBlocked"] = "True"
            },
            expectedScene: "BasementBrickRoom");

        Assert.IsFalse(result.Passed);
        CollectionAssert.Contains(result.ReasonCodes, BasementHallwayQaRouteAssertion.ReasonInputGateBlocked);
    }

    [Test]
    public void Evaluate_MissingExpectedScene_Fails()
    {
        BasementHallwayQaRouteAssertionResult result = BasementHallwayQaRouteAssertion.Evaluate(
            new Dictionary<string, string>
            {
                ["controllerFound"] = "False",
                ["activeScene"] = "BasementBrickRoom",
                ["transitionPending"] = "False",
                ["inputGateBlocked"] = "False"
            },
            expectedScene: null);

        Assert.IsFalse(result.Passed);
        CollectionAssert.Contains(result.ReasonCodes, BasementHallwayQaRouteAssertion.ReasonNoExpectedDestination);
    }
}
#endif
