#include <string.h>

extern "C" {

	void egmemcpy(void* s1, void* s2, size_t n)
	{
		memcpy(s1, s2, n);
	}
}
