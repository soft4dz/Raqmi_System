import { Router } from 'express';
import { backups } from '../data/in-memory-store';

export const adminBackupsRouter = Router();

adminBackupsRouter.get('/', (_request, response) => {
  response.json({ data: backups });
});
