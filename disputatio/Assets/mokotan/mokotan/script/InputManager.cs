using Fungus;
using UnityEngine;

public class InputManager : MonoBehaviour
{
    [SerializeField]
    DialogInput dialogInput;

    public void TurnOffEnable()
    {
        dialogInput.enabled = false;
    }

    public void TurnOnEnable()
    {
        dialogInput.enabled = true;
    }
    }
    
    

