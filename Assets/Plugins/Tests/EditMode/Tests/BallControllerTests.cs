using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class BallControllerTests
{
    private GameObject ballObject;
    private GameObject playerOneRacquetObject;
    private GameObject playerTwoRacquetObject;
    private Component ballController;
    private Type ballControllerType;

    [SetUp]
    public void SetUp()
    {
        ballControllerType = Type.GetType("CourtSmasherz.PickleballBallController, Assembly-CSharp");
        Assert.IsNotNull(ballControllerType, "Could not find PickleballBallController in Assembly-CSharp.");

        ballObject = new GameObject("Test Ball");
        Rigidbody rigidbody = ballObject.AddComponent<Rigidbody>();
        ballController = ballObject.AddComponent(ballControllerType);

        playerOneRacquetObject = new GameObject("P1 Racquet");
        playerTwoRacquetObject = new GameObject("P2 Racquet");

        playerOneRacquetObject.transform.position = new Vector3(-6f, 0f, 0f);
        playerTwoRacquetObject.transform.position = new Vector3(6f, 0f, 0f);

        SetField("playerOneRacquet", playerOneRacquetObject.transform);
        SetField("playerTwoRacquet", playerTwoRacquetObject.transform);
        SetField("rb", rigidbody);
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(ballObject);
        UnityEngine.Object.DestroyImmediate(playerOneRacquetObject);
        UnityEngine.Object.DestroyImmediate(playerTwoRacquetObject);
    }

    [Test]
    public void SpawnForServe_WhenPlayerOneServes_PlacesBallInFrontOfPlayerOneRacquet()
    {
        InvokePublicMethod("SpawnForServe", 0);

        Assert.AreEqual(new Vector3(-5.2f, 1f, 0f), ballObject.transform.position);
    }

    [Test]
    public void SpawnForServe_WhenPlayerTwoServes_PlacesBallInFrontOfPlayerTwoRacquet()
    {
        InvokePublicMethod("SpawnForServe", 1);

        Assert.AreEqual(new Vector3(5.2f, 1f, 0f), ballObject.transform.position);
    }

    [Test]
    public void CanFallbackServe_WhenBallIsSpawned_AllowsOnlyServingPlayer()
    {
        InvokePublicMethod("SpawnForServe", 0);

        Assert.IsTrue((bool)InvokePublicMethod("CanFallbackServe", 0));
        Assert.IsFalse((bool)InvokePublicMethod("CanFallbackServe", 1));
    }

    [Test]
    public void RegisterHit_WhenPlayerHits_DisablesFallbackServe()
    {
        InvokePublicMethod("SpawnForServe", 0);

        InvokePublicMethod("RegisterHit", 0);

        Assert.IsFalse((bool)InvokePublicMethod("CanFallbackServe", 0));
    }

    private void SetField(string fieldName, object value)
    {
        FieldInfo field = ballControllerType.GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );
        Assert.IsNotNull(field, $"Could not find field {fieldName}.");
        field.SetValue(ballController, value);
    }

    private object InvokePublicMethod(string methodName, params object[] args)
    {
        MethodInfo method = ballControllerType.GetMethod(methodName);
        Assert.IsNotNull(method, $"Could not find public method {methodName}.");
        return method.Invoke(ballController, args);
    }
}
