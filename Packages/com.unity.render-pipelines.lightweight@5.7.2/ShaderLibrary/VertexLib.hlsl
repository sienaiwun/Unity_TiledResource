#ifndef LIGHTWEIGHT_PIPELINE_VERTEXLIB_INCLUDED
#define LIGHTWEIGHT_PIPELINE_VERTEXLIB_INCLUDED

// 舞台特效
float _FrequencyStrength[64];
float _TimeScaleX;
float _TimeScaleY;
float _TimeScaleFrequency;
float _TilingX;
float _TilingY;
float _TilingFrequency;
float _BaseX;
float _BaseXEnlarge;
float _BaseY;
float _BaseYEnlarge;
float _BaseFrequency;
float _FrequencyAlpha;
float _TilingFadeX;
float _TilingFadeY;
float _FrequencyResist;

/* Property
_TimeScaleX("Time scale X", Range(-0.5, 0.5)) = -0.14
_TimeScaleY("Time scale Y", Range(-0.5, 0.5)) = -0.13
_TimeScaleFrequency("Time scale Frequency", Range(-0.5, 0.5)) = -0.17
_TilingX("Tiling X", Range(0, 10)) = 4.5
_TilingY("Tiling Y", Range(0, 10)) = 5.1
_TilingFrequency("Frequency Tiling", Range(0, 4)) = 1.8
_BaseX("Base X", Range(0, 10)) = 1.0
_BaseY("Base Y", Range(0, 10)) = 1.0
_BaseFrequency("Base Frequency", Range(0, 10)) = 1.0
_BaseYEnlarge("Base Y Enlarge", Range(2.0, 20)) = 12.5
_BaseXEnlarge("Base X Enlarge", Range(2.0, 20)) = 11.3
_FrequencyAlpha("Alpha Frequency", Range(0, 1)) = 1.0
_FrequencyResist("Frequency Resist", Range(0, 1)) = 0.5
_TilingFadeX("Tiling Fade X", Range(1, 10)) = 3.0
_TilingFadeY("Tiling Fade Y", Range(1, 10)) = 3.0
*/

static const float Primes[8] = { 1 / .2616, 1 / .2936, 1 / .3296, 1 / .3492, 1 / .392, 1 / .44, 1 / .4938, 1 / .53 };

float random(float t, float prime)
{
    return (cos(t * (prime - 0.2)) + cos(t * prime + prime) + cos(t * (prime + 0.2) + prime + 0.2)) / 3.0;
}

float GetSoundWaveOffset(float2 uv)
{
    float2 wave = uv;
    wave = float2(wave.x * sqrt(pow(_TilingFadeX, 1 - wave.x)), wave.y * sqrt(pow(_TilingFadeY, 1 - wave.y)));
    float X = (wave.x + _TimeScaleX * _Time.y) * _TilingX;
    float Y = (wave.y + _TimeScaleY * _Time.y) * _TilingY;
    float F = (X + Y + _TimeScaleFrequency * _Time.y) * _TilingFrequency;
				
    float XSoundWave = 0;
    for (
        int index = 0; index < 8; index++)
    {
        XSoundWave += _FrequencyStrength[index] * random(F, Primes[index]) * _BaseFrequency;
    }
    
    float positionOffset = (sin(X * 2.2) * cos(X * 1.3) * _BaseX * (log(lerp(2.0, _BaseXEnlarge, uv.x)) - 0.5) + _BaseY * sin(Y * 2.7) * cos(Y * 1.1) * (log(lerp(2.0, _BaseYEnlarge, uv.y)) - 0.5));
					
    return lerp((1 + lerp(0, XSoundWave, _FrequencyAlpha)), 1, _FrequencyResist * sqrt(abs(positionOffset))) * positionOffset;
}
// - 舞台特效
#endif

		