***"Monkey brain see that light get darker as energy used." - Me, just now***

# Fading Flashlights

Lethal Company's flashlights are fun, but they are mostly static. Let's fix that.

With this mod, over time your flashlight will dim in brightness. By default, either type of flashlight starts to fade at 50% battery,
starting slowly, but dimming faster and faster over time, finally reaching no light at no battery.

# Config

## Fade Start

A number between 0 and 1, representing the percentage of the flashlight used before it starts to fade. For example, a value of 0.5
means that it will start fading at 50%.

*Default: 0.5*

## Fade Final Brightness

A number between 0 and 1, representing the percentage of brightness the flashlight will run out with. For example, a value of 0.5
means that the flashlight will end with a brightness that is 50% of the original.

*Default: 0.5*

## Fade Function Exponent

Any number is acceptable.

### I am a math nerd

The above two config options create a linear line, that goes through points (0, FadeFinalBrightness), and ending at (FadeStart, 1).
Then, we put the output of that line at the specified charge value for x^(2^FadeFunctionExponent).

**This means that if you put in -1, you take the square root. If you put in -2, you are doing the fourth root. If you put in 1, your
square, and if you put in 2, you put it to the power of four. If you put in 0, the brightness will linearly decrease over time.**

### I am normal

This affects how quickly your flashlight runs out of battery.

If you put in a negative number, it will start staying brighter for longer, but the brightness will decrease faster and faster over time.
If you put in a positive number, it will start decreasing quickly, but the rate will slow down over time.
If you put in zero, the brightness will decrease at the same rate over time.


*Default: -1 (square root)*


# Credits
I wrote all of the code myself, and drew the logo, but that doesn't mean I got no help.

Mommyplier - Help with Programming, Math, and the Logo
Braydon3DS - Emotional Support and distracting me with *Ratchet and Clank*
TheRealMutt - Not believing that I could create a mod
