
namespace Manager;

public interface ICacheManager
    {
        bool Create(string key, object value);
        object Read(string key);
        bool Update(string key, object value);
        bool Delete(string key);
    }

