using System;
namespace e.l.f._Beauty.Services.Sorting
{
	public interface ISorting
	{
        IBrewerySorter Create(string sortType);
    }
}

