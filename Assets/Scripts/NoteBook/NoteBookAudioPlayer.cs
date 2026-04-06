using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static AudioManager;

public class NoteBookAudioPlayer : MonoBehaviour
{
    public void NoteBookInteractionSound()
    {
        int myRandomNum = Random.Range(0, 5);
        if (myRandomNum == 0)
        {
            AudioManager.Play("Sound Effects/Chapter1/sndNoteBook1", AudioGroup.SFX);
        }
        else if (myRandomNum == 1)
        {
            AudioManager.Play("Sound Effects/Chapter1/sndNoteBook2", AudioGroup.SFX);
        }
        else if (myRandomNum == 2)
        {
            AudioManager.Play("Sound Effects/Chapter1/sndNoteBook3", AudioGroup.SFX);
        }
        else if (myRandomNum == 3)
        {
            AudioManager.Play("Sound Effects/Chapter1/sndNoteBook4", AudioGroup.SFX);
        }
        else if (myRandomNum == 4)
        {
            AudioManager.Play("Sound Effects/Chapter1/sndNoteBook5", AudioGroup.SFX);
        }
        else if (myRandomNum == 5)
        {
            AudioManager.Play("Sound Effects/Chapter1/sndNoteBook6", AudioGroup.SFX);
        }
    }
}