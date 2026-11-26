using UnityEngine;
using Oculus.Interaction;

public class WedgeButtonInteraction : MonoBehaviour
{
    private PointableUnityEventWrapper _pokeButton;

    void Awake()
    {
        _pokeButton = GetComponent<PointableUnityEventWrapper>();
    }

    void OnEnable()
    {
        //_pokeButton.WhenSelect.AddListener();
    }

    void OnDisable()
    {
        //_pokeButton.WhenSelect.RemoveListener();
    }
}
