import { userList, userCreate, userUpdate, userDelete } from "~~/generated/rivet/client/reel";
import type { User } from "~~/generated/rivet/types/domain";

const STORAGE_KEY = "reel:userId";

export const useUser = () => {
  const currentUserId = useState<number>("reel:userId", () => {
    const stored = localStorage.getItem(STORAGE_KEY);
    return stored ? Number(stored) : 1;
  });

  const users = useState<User[]>("reel:users", () => []);

  const persistUserId = (id: number) => {
    currentUserId.value = id;
    localStorage.setItem(STORAGE_KEY, String(id));
  };

  const fetchUsers = async () => {
    users.value = await userList();
  };

  const switchUser = (id: number) => {
    persistUserId(id);
  };

  const createNewUser = async (name: string, icon?: string) => {
    await userCreate({ name, icon: icon ?? null });
    await fetchUsers();
  };

  const updateExistingUser = async (id: number, name: string, icon?: string) => {
    await userUpdate(String(id), { name, icon: icon ?? null });
    await fetchUsers();
  };

  const deleteExistingUser = async (id: number) => {
    await userDelete(String(id));
    await fetchUsers();
  };

  return {
    currentUserId: readonly(currentUserId),
    users: readonly(users),
    fetchUsers,
    switchUser,
    createUser: createNewUser,
    updateUser: updateExistingUser,
    deleteUser: deleteExistingUser,
  };
};
