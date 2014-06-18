unity-countly
=============

unity-countly is a [Unity 3D](http://unity3d.com) plugin for facilitating
integration with [Countly](http://count.ly) Mobile Analytics.

## Why?

Although there is
[countly-sdk-unity](https://github.com/Countly/countly-sdk-unity), I believe it
does not match the quality of the iOS and Android versions provided by
[Countly Team](https://github.com/Countly).


unity-countly provides the following advantages over countly-sdk-unity:

- More robust (for instance if you run ```Countly.Instance.OnStart()```,
  ```Countly.Instance.OnStop()``` more than once you will get errors).
- Countly SDK 2.0 compliant.
- Understands when your app goes to background and comes to foreground.
- Events that could not be delivered are stored so that they can be delivered
  next time your app starts.
- Detects mobile carrier name.
- Detects locale properly.

## Requirements

* Unity 3.x Pro or above.

## Change Log

The [change log](https://github.com/imkira/unity-countly/releases/)
contains the list of changes and latest version information of each package.

# Download Package

[Download v0.0.1](https://github.com/imkira/unity-countly/releases/)

## How To Integrate

* Create a ```CountlyManager``` object in your scene.
* Add the ```Assets/Plugins/Countly/CountlyManager.cs``` component to it.
* Set the App Key parameter.

If you leave ```App Key``` blank, you can initialize Countly at any time
using ```CountlyManager.Init("your_app_key_here"`);```

## Emitting Events

```
CountlyManager.Emit("dummy_event", 1);
```

or

```
double count = 5.0;
CountlyManager.Emit("tap", 1, duration);
```

or

```
CountlyManager.Emit("clear", 1,
  new Dictionary<string, string>()
  {
    {"level", "1"},
    {"difficulty", "normal"}
  });
```

or

```
double price = 123.4;
CountlyManager.Emit("purchase", 1, price,
  new Dictionary<string, string>()
  {
    {"purchase_id", "product01"},
  });
```

or ultimately


```
Countly.Event e = new Countly.Event();

e.Key = "purchase";
e.Count = 1;
e.Sum  = 123.4;
e.Segmentation =
  new Dictionary<string, string>()
  {
    {"purchase_id", "product01"},
  });

CountlyManager.Emit(e);
```

## Contribute

* Found a bug?
* Want to contribute and add a new feature?

Please fork this project and send me a pull request!

## License

unity-countly is licensed under the MIT license:

www.opensource.org/licenses/MIT

## Copyright

Copyright (c) 2014 Mario Freitas. See
[LICENSE.txt](http://github.com/imkira/unity-countly/blob/master/LICENSE.txt)
for further details.
