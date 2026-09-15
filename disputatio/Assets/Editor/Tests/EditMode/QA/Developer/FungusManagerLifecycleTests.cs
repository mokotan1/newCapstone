#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Reflection;
using Fungus;
using NUnit.Framework;

public class FungusManagerLifecycleTests
{
    [Test]
    public void ResetStatics_AfterPreviousPlaySession_AllowsSingletonCreationAgain()
    {
        const BindingFlags StaticNonPublic =
            BindingFlags.Static | BindingFlags.NonPublic;
        FieldInfo quittingField = typeof(FungusManager).GetField(
            "applicationIsQuitting",
            StaticNonPublic);
        FieldInfo instanceField = typeof(FungusManager).GetField(
            "instance",
            StaticNonPublic);
        MethodInfo resetMethod = typeof(FungusManager).GetMethod(
            "ResetStatics",
            StaticNonPublic);

        Assert.IsNotNull(quittingField);
        Assert.IsNotNull(instanceField);
        Assert.IsNotNull(
            resetMethod,
            "FungusManager must reset its singleton guards when domain reload is disabled.");

        object originalInstance = instanceField.GetValue(null);
        object originalQuitting = quittingField.GetValue(null);
        try
        {
            quittingField.SetValue(null, true);
            resetMethod.Invoke(null, null);

            Assert.IsFalse((bool)quittingField.GetValue(null));
        }
        finally
        {
            instanceField.SetValue(null, originalInstance);
            quittingField.SetValue(null, originalQuitting);
        }
    }
}
#endif
