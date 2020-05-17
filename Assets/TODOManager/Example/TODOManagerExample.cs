//This comment doesn't start with the identifier, so it's ignored
using UnityEngine;

public class TODOManagerExample : MonoBehaviour {
    /*
           TODO: This comment starts with one of the identifiers, so it will be picked up and displayed 
    */

    //TODO #High importance, @Person add the field values required for the object

    // The bellow string sequence is ignored by the default profile
    private string skipExample = "This text contains a search region start sequence \"/*\" and the end sequence \"*/\" but because it's set to ignore the control character \\ and ignore string sequences, it skips over the values and ignores them. Even a //TODO: Statement #Ignored";
}
