package org.garret.perst.impl;
import  org.garret.perst.*;

import java.lang.reflect.*;
import java.util.Date;
import java.util.ArrayList;

class BtreeFieldIndex extends Btree implements FieldIndex { 
    String className;
    String fieldName;
    long   autoincCount;
    transient Class cls;
    transient Field fld;

    BtreeFieldIndex() {}
    
    private final void locateField() 
    {
        Class scope = cls;
        try { 
            do { 
                try { 
                    fld = scope.getDeclaredField(fieldName);                
                    fld.setAccessible(true);
                    break;
                } catch (NoSuchFieldException x) { 
                    scope = scope.getSuperclass();
                }
            } while (scope != null);
        } catch (Exception x) {
            throw new StorageError(StorageError.ACCESS_VIOLATION, className + "." + fieldName, x);
        }
        if (fld == null) { 
           throw new StorageError(StorageError.INDEXED_FIELD_NOT_FOUND, className + "." + fieldName);
        }
    }

    public void onLoad()
    {
        try { 
            cls = Class.forName(className);
        } catch (Exception x) { 
            throw new StorageError(StorageError.CLASS_NOT_FOUND, className, x);
        }           
        locateField();
    }

    BtreeFieldIndex(Class cls, String fieldName, boolean unique) {
        this.cls = cls;
        this.unique = unique;
        this.fieldName = fieldName;
        this.className = cls.getName();
        locateField();
        type = ClassDescriptor.getTypeCode(fld.getType());
        if (type >= ClassDescriptor.tpLink) { 
            throw new StorageError(StorageError.UNSUPPORTED_INDEX_TYPE, fld.getType());
        }
    }

    private Key extractKey(IPersistent obj) { 
        try { 
            Field f = fld;
            Key key = null;
            switch (type) {
              case ClassDescriptor.tpBoolean:
                key = new Key(f.getBoolean(obj));
                break;
              case ClassDescriptor.tpByte:
                key = new Key(f.getByte(obj));
                break;
              case ClassDescriptor.tpShort:
                key = new Key(f.getShort(obj));
                break;
              case ClassDescriptor.tpChar:
                key = new Key(f.getChar(obj));
                break;
              case ClassDescriptor.tpInt:
                key = new Key(f.getInt(obj));
                break;            
              case ClassDescriptor.tpObject:
                key = new Key((IPersistent)f.get(obj));
                break;
              case ClassDescriptor.tpLong:
                key = new Key(f.getLong(obj));
                break;            
              case ClassDescriptor.tpDate:
                key = new Key((Date)f.get(obj));
                break;
              case ClassDescriptor.tpFloat:
                key = new Key(f.getFloat(obj));
                break;
              case ClassDescriptor.tpDouble:
                key = new Key(f.getDouble(obj));
                break;
              case ClassDescriptor.tpString:
                key = new Key((String)f.get(obj));
                break;
              default:
                Assert.failed("Invalid type");
            }
            return key;
        } catch (Exception x) { 
            throw new StorageError(StorageError.ACCESS_VIOLATION, x);
        }
    }
            

    public boolean put(IPersistent obj) {
        return super.insert(extractKey(obj), obj, false);
    }

    public void set(IPersistent obj) {
         super.insert(extractKey(obj), obj, true);
    }

    public void  remove(IPersistent obj) {
        super.remove(new BtreeKey(extractKey(obj), obj.getOid()));
    }

    public synchronized void append(IPersistent obj) {
        Key key;
        try { 
            switch (type) {
              case ClassDescriptor.tpInt:
                key = new Key((int)autoincCount);
                fld.setInt(obj, (int)autoincCount);
                break;            
              case ClassDescriptor.tpLong:
                key = new Key(autoincCount);
                fld.setLong(obj, autoincCount);
                break;            
              default:
                throw new StorageError(StorageError.UNSUPPORTED_INDEX_TYPE, fld.getType());
            }
        } catch (Exception x) { 
            throw new StorageError(StorageError.ACCESS_VIOLATION, x);
        }
        autoincCount += 1;
        obj.modify();
        super.insert(key, obj, false);
    }

    public IPersistent[] get(Key from, Key till) {
        if ((from != null && from.type != type) || (till != null && till.type != type)) { 
            throw new StorageError(StorageError.INCOMPATIBLE_KEY_TYPE);
        }
        ArrayList list = new ArrayList();
        if (root != 0) { 
            BtreePage.find((StorageImpl)getStorage(), root, from, till, type, height, list);
        }
        return (IPersistent[])list.toArray((Object[])Array.newInstance(cls, list.size()));
    }

    public IPersistent[] toPersistentArray() {
        IPersistent[] arr = (IPersistent[])Array.newInstance(cls, nElems);
        if (root != 0) { 
            BtreePage.traverseForward((StorageImpl)getStorage(), root, type, height, arr, 0);
        }
        return arr;
    }
}

