using System;
using System.Collections.Generic;

namespace Twinkly_xled
{
    internal class TwinklyComparer : IEqualityComparer<TwinklyInstance>
    {
        public bool Equals(TwinklyInstance x, TwinklyInstance y)
        {
            //Check whether the compared objects reference the same data.
            if (Object.ReferenceEquals(x, y)) return true;

            //Check whether any of the compared objects is null.
            if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null))
                return false;

            //Check whether the ip address properties are equal.
            return x.Address == y.Address;
        }

        // If Equals() returns true for a pair of objects
        // then GetHashCode() must return the same value for these objects.

        public int GetHashCode(TwinklyInstance twink)
        {
            //Check whether the object is null
            if (Object.ReferenceEquals(twink, null)) return 0;

            //Get hash code for the Name field if it is not null.
            int hashTwinklyName = twink.Name == null ? 0 : twink.Name.GetHashCode();

            //Get hash code for the Code field.
            int hashTwinklyAddress = twink.Address.GetHashCode();

            //Calculate the hash code for the product.
            return hashTwinklyName ^ hashTwinklyAddress;
        }
    }
}
