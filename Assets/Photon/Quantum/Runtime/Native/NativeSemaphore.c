#ifdef __ANDROID__
void* QSemaphore_Create() { return 0; }
void  QSemaphore_Wait(void* s) { }
void  QSemaphore_Post(void *s) { }
#else
#ifdef _MSC_VER
void* QSemaphore_Create() { return 0; }
void  QSemaphore_Wait(void* s) { }
void  QSemaphore_Post(void *s) { }
#else
#include "string.h"
#include "stdlib.h"

#ifdef __APPLE__
#include <dispatch/dispatch.h>
#else
#import "semaphore.h"
#endif

struct QuantumSemaphore 
{
#ifdef __APPLE__
    dispatch_semaphore_t    sem;
#else
    sem_t                   sem;
#endif
};

void QSemaphore_Free(struct QuantumSemaphore *s) 
{
    free(s);
}

void* QSemaphore_Create()
{
    void* memory = malloc(sizeof(struct QuantumSemaphore));
    memset(memory, 0, sizeof(struct QuantumSemaphore));

#ifdef __APPLE__
    struct QuantumSemaphore* semaphore = (struct QuantumSemaphore*)memory;
    semaphore->sem = dispatch_semaphore_create(0);
#else
    sem_init(&semaphore->sem, 0, 0);
#endif

    return semaphore;
}

void QSemaphore_Wait(struct QuantumSemaphore *s)
{
#ifdef __APPLE__
    dispatch_semaphore_wait(s->sem, DISPATCH_TIME_FOREVER);
#else
    int r;
    
    do {
        r = sem_wait(&s->sem);
    } while (r == -1 && errno == EINTR);
#endif
}

void QSemaphore_Post(struct QuantumSemaphore *s)
{
#ifdef __APPLE__
    dispatch_semaphore_signal(s->sem);
#else 
    sem_post(&s->sem);
#endif
}
#endif
#endif