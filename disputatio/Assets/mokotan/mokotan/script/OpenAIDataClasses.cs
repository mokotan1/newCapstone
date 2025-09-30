// OpenAIDataClasses.cs

using System.Collections.Generic; // List를 사용하기 위해 필요

[System.Serializable]
public class OpenAIMessage
{
    public string role;
    public string content;
}

[System.Serializable]
public class OpenAIPayload
{
    public string model;
    public List<OpenAIMessage> messages;
}

[System.Serializable]
public class OpenAIChoice
{
    public OpenAIMessage message;
}

[System.Serializable]
public class OpenAIResponse
{
    public List<OpenAIChoice> choices;
}