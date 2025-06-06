# FuzzPhyte Unity Tools

## Utility

FP_Utility is designed and built to be a simple set of base classes to be used in almost all future FuzzPhyte packages. There is an element of Scriptable Object and an element of just simple input/output functions as well as some core scripts timed to timers etc. There are a lot of static functions to help with file management and Unity Editor management. Please see the FP_UtilityData class as well as the FP_Utility_Editor class for a lot of these functions/enums/structs etc.

## Setup & Design

FP_Utility is not much by itself and is designed to allow extensions and/or inheritance for other work. An example of this is the FP_Notification.cs file. This is a super simple class that a lot of other projects will be derived from.

SamplesURP will require additional package imports.

* com.unity.render-pipelines.universal

### Software Architecture

FP_Utility has a core data class for ScriptableObjects called FP_Data. This is heavily used for all generic data classes and in other packages there could be further extension of this for generic ScriptableObjects that need a sort of UniqueID. There are additional sub-folders by domain areas. For example, there is a simple IK manager script located in the FuzzPhyte.Utility.Animation namespace. Some of these sub-folders contain their own domain assembly.

### Ways to Extend

## Dependencies

Please see the [package.json](./package.json) file for more information.

## License Notes

* This software running a dual license
* Most of the work this repository holds is driven by the development process from the team over at Unity3D :heart: to their never ending work on providing fantastic documentation and tutorials that have allowed this to be born into the world.
* I personally feel that software and it's practices should be out in the public domain as often as possible, I also strongly feel that the capitalization of people's free contribution shouldn't be taken advantage of.
  * If you want to use this software to generate a profit for you/business I feel that you should equally 'pay up' and in that theory I support strong copyleft licenses.
  * If you feel that you cannot adhere to the GPLv3 as a business/profit please reach out to me directly as I am willing to listen to your needs and there are other options in how licenses can be drafted for specific use cases, be warned: you probably won't like them :rocket:

### Educational and Research Use MIT Creative Commons

* If you are using this at a Non-Profit and/or are you yourself an educator and want to use this for your classes and for all student use please adhere to the MIT Creative Commons License
* If you are using this back at a research institution for personal research and/or funded research please adhere to the MIT Creative Commons License
  * If the funding line is affiliated with an [SBIR](https://www.sbir.gov) be aware that when/if you transfer this work to a small business that work will have to be moved under the secondary license as mentioned below.

### Commercial and Business Use GPLv3 License

* For commercial/business use please adhere by the GPLv3 License
* Even if you are giving the product away and there is no financial exchange you still must adhere to the GPLv3 License

## Contact

* [John Shull](mailto:JShull@fuzzphyte.com)

### Additional Notes

* Audio Files in the samples came from [FreeSound.org](https://freesound.org/)
